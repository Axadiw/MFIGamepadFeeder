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
        public int? CustomIndex { get; set; }
        
        // a GamepadMappingItem can have a value like 2, 32, 64, etc.
        // when multiple buttons are pressed on one index, the value is the sum of the pressed buttons
        public int? CustomValue { get; set; }
    }
}