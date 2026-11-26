namespace MFIGamepadFeeder.Core.Models;

/// <summary>
/// XInput 16-bit gamepad button bitmask (matches XINPUT_GAMEPAD.wButtons and legacy .mfimapping files).
/// </summary>
[Flags]
public enum XInputButtons : ushort
{
    None = 0x0000,
    DpadUp = 0x0001,
    DpadDown = 0x0002,
    DpadLeft = 0x0004,
    DpadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftStick = 0x0040,
    RightStick = 0x0080,
    LBumper = 0x0100,
    RBumper = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>
/// Matches the exact numeric values stored in legacy .mfimapping files.
/// </summary>
public enum AxisType : byte
{
    Rx = 0,
    Ry = 1,
    Lx = 2,
    Ly = 3,
    LTrigger = 4,
    RTrigger = 5,
}

/// <summary>
/// Complete atomic snapshot of a virtual Xbox 360 controller state.
/// Submitted in a single bus report to eliminate input tearing, D-pad flicker, and multi-IOCTL latency.
/// </summary>
public readonly record struct GamepadState(
    XInputButtons Buttons,
    byte LeftTrigger,
    byte RightTrigger,
    short LeftThumbX,
    short LeftThumbY,
    short RightThumbX,
    short RightThumbY)
{
    public static GamepadState Neutral => default;
}
