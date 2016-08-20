namespace MFIGamepadFeeder.Gamepads.Configuration
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
        public uint? TargetUsage { get; set; }
        public uint? TargetButtonId { get; set; }
    }
}