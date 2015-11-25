namespace MFIGamepadFeeder
{
    public enum GamepadItemType
    {
        Axis,
        Button,
        DPadUp,
        DPadRight,
        DPadDown,
        DPadLeft,
        Empty
    }

    public class GamepadConfigurationItem
    {
        public GamepadItemType Type { get; set; }
        public bool? InvertAxis { get; set; }
        public bool? ConvertAxis { get; set; }
        public HID_USAGES? TargetUsage { get; set; }
        public uint? TargetButtonId { get; set; }
    }
}