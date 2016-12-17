using vGenWrapper;

namespace MFIGamepadShared.Configuration
{
    public enum GamepadItemType
    {
        Axis,
        Button,
        DPad,
        Empty
    }

    public class GamepadConfigurationItem
    {
        public GamepadItemType Type { get; set; }
        public bool? InvertAxis { get; set; }
        public bool? ConvertAxis { get; set; }
        public XInputGamepadButtons? ButtonType { get; set; }
        public XInputGamepadDPadButtons? DPadType { get; set; }
        public AxisType? AxisType { get; set; }
    }
}