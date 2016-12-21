using vGenWrapper;

namespace MFIGamepadShared.Configuration
{
    public enum GamepadMappingItemType
    {
        Axis,
        Button,
        DPad,
        Empty
    }

    public class GamepadMappingItem
    {
        public GamepadMappingItem()
        {
            Type = GamepadMappingItemType.Empty;
            InvertAxis = false;
            ConvertAxis = false;
            ButtonType = null;
            AxisType = null;
        }


        public GamepadMappingItemType Type { get; set; }
        public bool? InvertAxis { get; set; }
        public bool? ConvertAxis { get; set; }
        public XInputGamepadButtons? ButtonType { get; set; }
        public AxisType? AxisType { get; set; }
    }
}