using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace MFIGamepadShared.Configuration
{
    public class GamepadConfiguration
    {
        public readonly uint GamepadId;
        public readonly HidDeviceRepresentation HidDevice;
        public readonly Collection<GamepadMappingItem> MappingItems;

        public GamepadConfiguration(uint gamepadId, string mappingJsonRepresentation, HidDeviceRepresentation hidDevice)
        {
            GamepadId = gamepadId;
            HidDevice = hidDevice;
            var loadedConfig = JsonConvert.DeserializeObject<Collection<GamepadMappingItem> >(mappingJsonRepresentation);
            MappingItems = loadedConfig;
        }        
    }
}