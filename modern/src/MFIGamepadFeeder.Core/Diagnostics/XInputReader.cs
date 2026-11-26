using System.Runtime.InteropServices;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Diagnostics;

/// <summary>Reads existing XInput controllers without creating or changing a device.</summary>
public static class XInputReader
{
    /// <summary>
    /// Reads XInput slot 0..3. Returns false with neutral state when unavailable or disconnected.
    /// Values are unfiltered: positive stick Y means up; documented XInputGetState omits Guide.
    /// </summary>
    public static bool TryRead(uint slotIndexZeroBased, out GamepadState state)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slotIndexZeroBased, 3u);
        state = GamepadState.Neutral;

        try
        {
            if (XInputGetState(slotIndexZeroBased, out var native) != 0)
            {
                return false;
            }

            // XINPUT_GAMEPAD defines 0xF3FF; other button bits are reserved and undefined.
            state = new GamepadState(
                (XInputButtons)(native.Buttons & 0xF3FF),
                native.LeftTrigger,
                native.RightTrigger,
                native.LeftThumbX,
                native.LeftThumbY,
                native.RightThumbX,
                native.RightThumbY);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    // XINPUT_STATE: DWORD packet number followed by the 12-byte XINPUT_GAMEPAD.
    // https://learn.microsoft.com/windows/win32/api/xinput/ns-xinput-xinput_state
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint XInputGetState(uint dwUserIndex, out XInputState pState);
}
