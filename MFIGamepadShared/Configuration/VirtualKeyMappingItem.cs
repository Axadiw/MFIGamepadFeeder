using System.Collections.Generic;
using System.Collections.ObjectModel;
using vGenWrapper;

namespace MFIGamepadShared.Configuration
{
    public class VirtualKeyMappingItem
    {
        public List<XInputGamepadButtons?> SourceKeys { get; set; }
        public XInputGamepadButtons? DestinationItem { get; set; }

        public VirtualKeyMappingItem()
        {
            SourceKeys = new List<XInputGamepadButtons?>();
            DestinationItem = null;
        }

        public VirtualKeyMappingItem(List<XInputGamepadButtons?> sourceKeys, XInputGamepadButtons? destinationItem)
        {
            SourceKeys = sourceKeys;
            DestinationItem = destinationItem;
        }
    }
}