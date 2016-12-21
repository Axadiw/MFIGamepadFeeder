using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MFIGamepadShared.Configuration
{
    public class GamepadMapping
    {
        public readonly IList<GamepadMappingItem> MappingItems;

        public GamepadMapping(IList<GamepadMappingItem> mappingItems)
        {
            MappingItems = mappingItems;
        }
    }
}
