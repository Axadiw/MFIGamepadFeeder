using System.Collections.Generic;

namespace MFIGamepadShared.Configuration
{
    public class GamepadMapping
    {
        public readonly IList<GamepadMappingItem> MappingItems;
        public readonly IList<VirtualKeyMappingItem> VirtualKeysItems;

        public GamepadMapping()
        {
            MappingItems = new List<GamepadMappingItem>();
            VirtualKeysItems = new List<VirtualKeyMappingItem>();
        }

        public GamepadMapping(IList<GamepadMappingItem> mappingItems, IList<VirtualKeyMappingItem> virtaulKeysItems)
        {
            MappingItems = mappingItems;
            VirtualKeysItems = virtaulKeysItems;
        }
    }
}