using System;

namespace vGenWrapper
{
    [Flags]
    public enum XInputGamepadButtons : uint
    {
        Start = 0x0010,
        Back = 0x0020,
        LeftStick = 0x0040,
        RightStick = 0x0080,
        LBumper = 0x0100,
        RBumper = 0x0200,
        Guide = 0x0400,
        LTrigger = 0x10000,
        RTrigger = 0x20800,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
        DpadUp = 0x0001,
        DpadDown = 0x0002,
        DpadLeft = 0x0004,
        DpadRight = 0x0008,
        All = Start | Back | LeftStick | RightStick | LBumper | RBumper | A | B | X | Y | DpadUp | DpadDown | DpadLeft| DpadRight | LTrigger | RTrigger | Guide
    }

    public enum AxisType : uint
    {
        Rx = 8,
        Ry = 9,
        Lx = 4,
        Ly = 5,
        LTrigger = 0x1000,
        RTrigger = 0x2000
    }
}
