using HidSharp;

namespace MFIGamepadFeeder.Core.Hid;

public sealed class HidSharpReportSource : IHidReportSource
{
    private readonly HidDeviceInfo _target;
    private readonly int _readTimeoutMs;

    public HidSharpReportSource(HidDeviceInfo target, int readTimeoutMs = 100)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _readTimeoutMs = Math.Clamp(readTimeoutMs, 20, 2000);
    }

    public string DisplayName => _target.DisplayName;

    public bool TryOpen(out IHidReportStream? stream, out string? errorMessage)
    {
        stream = null;
        errorMessage = null;

        try
        {
            var devices = DeviceList.Local.GetHidDevices(_target.VendorId, _target.ProductId).ToList();
            if (devices.Count == 0)
            {
                errorMessage = $"HID device VID:0x{_target.VendorId:X4} PID:0x{_target.ProductId:X4} is currently not connected.";
                return false;
            }

            // Prefer exact DevicePath match, otherwise fall back to matching report length / gamepad usage
            // (Windows Bluetooth reconnects can alter the HID instance path suffix).
            // Never throw if a sibling HID collection (e.g. battery/vendor) fails GetMaxInputReportLength().
            var device =
                devices.FirstOrDefault(d => string.Equals(d.DevicePath, _target.DevicePath, StringComparison.OrdinalIgnoreCase))
                ?? devices.FirstOrDefault(d => TryGetMaxInputReportLength(d) == _target.MaxInputReportLength)
                ?? devices.FirstOrDefault(d => HidDeviceService.HasGamepadOrJoystickUsage(d) && TryGetMaxInputReportLength(d) >= 4)
                ?? devices.FirstOrDefault(d => TryGetMaxInputReportLength(d) > 0);

            if (device is null)
            {
                errorMessage = $"HID device VID:0x{_target.VendorId:X4} PID:0x{_target.ProductId:X4} has no readable input report interface.";
                return false;
            }

            if (!device.TryOpen(out var hidStream))
            {
                errorMessage = "Device found, but failed to open HID stream (it may be held exclusively by another process).";
                return false;
            }

            var maxIn = Math.Max(1, TryGetMaxInputReportLength(device));
            hidStream.ReadTimeout = _readTimeoutMs;
            stream = new HidSharpReportStream(hidStream, maxIn);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static int TryGetMaxInputReportLength(HidDevice device)
    {
        try
        {
            return device.GetMaxInputReportLength();
        }
        catch
        {
            return 0;
        }
    }

    private sealed class HidSharpReportStream : IHidReportStream
    {
        private readonly HidStream _stream;
        private bool _disposed;

        public HidSharpReportStream(HidStream stream, int maxInputReportLength)
        {
            _stream = stream;
            MaxInputReportLength = maxInputReportLength;
        }

        public int MaxInputReportLength { get; }

        public HidReadResult Read(byte[] buffer, CancellationToken cancellationToken)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0);
            }

            try
            {
                var count = _stream.Read(buffer, 0, Math.Min(buffer.Length, MaxInputReportLength));
                return count > 0
                    ? new HidReadResult(HidReadResultKind.Data, count)
                    : new HidReadResult(HidReadResultKind.Disconnected, 0, "HID stream returned 0 bytes (EOF).");
            }
            catch (TimeoutException)
            {
                return new HidReadResult(HidReadResultKind.Timeout, 0);
            }
            catch (ObjectDisposedException)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0);
            }
            catch (Exception ex)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0, ex.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _stream.Dispose();
            }
            catch
            {
                // Ignore stream close errors during disconnect/shutdown.
            }
        }
    }
}
