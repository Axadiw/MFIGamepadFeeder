using MFIGamepadFeeder.Core.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace MFIGamepadFeeder.Core.Backends;

/// <summary>
/// Modern virtual Xbox 360 backend powered by ViGEmBus (HVCI/Windows 11 compatible).
/// Uses AutoSubmitReport = false so every HID update is submitted in ONE atomic kernel report,
/// and virtual controllers unplug automatically on process exit or crash.
/// </summary>
public sealed class ViGEmBackend : IVirtualGamepadBackend
{
    private ViGEmClient? _client;

    public string Name => "ViGEmBus";
    public bool IsLegacyDriver => false;

    public bool IsAvailable()
    {
        if (_client is not null)
        {
            return true;
        }

        try
        {
            using var probe = new ViGEmClient();
            return true;
        }
        catch (VigemBusNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public IVirtualController ConnectController(uint slotIndex)
    {
        if (slotIndex is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Controller slot must be between 1 and 4.");
        }

        _client ??= new ViGEmClient();
        var controller = _client.CreateXbox360Controller();
        controller.AutoSubmitReport = false;
        controller.Connect();

        var wrapper = new ViGEmXbox360Controller(controller, slotIndex);
        try
        {
            wrapper.Reset();
            return wrapper;
        }
        catch
        {
            wrapper.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    private sealed class ViGEmXbox360Controller : IVirtualController
    {
        private readonly IXbox360Controller _controller;
        private readonly object _lock = new();
        private bool _disposed;

        public ViGEmXbox360Controller(IXbox360Controller controller, uint slotIndex)
        {
            _controller = controller;
            SlotIndex = slotIndex;
        }

        public string BackendName => "ViGEmBus";
        public uint SlotIndex { get; }

        public void SubmitState(in GamepadState state)
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                SubmitStateLocked(state);
            }
        }

        private void SubmitStateLocked(in GamepadState state)
        {
            var b = state.Buttons;
            _controller.SetButtonState(Xbox360Button.Up, (b & XInputButtons.DpadUp) != 0);
            _controller.SetButtonState(Xbox360Button.Down, (b & XInputButtons.DpadDown) != 0);
            _controller.SetButtonState(Xbox360Button.Left, (b & XInputButtons.DpadLeft) != 0);
            _controller.SetButtonState(Xbox360Button.Right, (b & XInputButtons.DpadRight) != 0);
            _controller.SetButtonState(Xbox360Button.Start, (b & XInputButtons.Start) != 0);
            _controller.SetButtonState(Xbox360Button.Back, (b & XInputButtons.Back) != 0);
            _controller.SetButtonState(Xbox360Button.LeftThumb, (b & XInputButtons.LeftStick) != 0);
            _controller.SetButtonState(Xbox360Button.RightThumb, (b & XInputButtons.RightStick) != 0);
            _controller.SetButtonState(Xbox360Button.LeftShoulder, (b & XInputButtons.LBumper) != 0);
            _controller.SetButtonState(Xbox360Button.RightShoulder, (b & XInputButtons.RBumper) != 0);
            _controller.SetButtonState(Xbox360Button.Guide, (b & XInputButtons.Guide) != 0);
            _controller.SetButtonState(Xbox360Button.A, (b & XInputButtons.A) != 0);
            _controller.SetButtonState(Xbox360Button.B, (b & XInputButtons.B) != 0);
            _controller.SetButtonState(Xbox360Button.X, (b & XInputButtons.X) != 0);
            _controller.SetButtonState(Xbox360Button.Y, (b & XInputButtons.Y) != 0);

            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, state.LeftTrigger);
            _controller.SetSliderValue(Xbox360Slider.RightTrigger, state.RightTrigger);

            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, state.LeftThumbX);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, state.LeftThumbY);
            _controller.SetAxisValue(Xbox360Axis.RightThumbX, state.RightThumbX);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, state.RightThumbY);

            // Single atomic kernel report submission.
            _controller.SubmitReport();
        }

        public void Reset()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                SubmitStateLocked(GamepadState.Neutral);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    SubmitStateLocked(GamepadState.Neutral);
                }
                catch
                {
                    // Best-effort reset before disconnect.
                }

                try
                {
                    _controller.Disconnect();
                }
                catch
                {
                    // Ignore disconnect errors if bus already vanished.
                }

                _disposed = true;
            }
        }
    }
}
