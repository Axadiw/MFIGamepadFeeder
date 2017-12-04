using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace MFIGamepadShared.Configuration
{
    public class GamepadConfiguration
    {
        public readonly uint GamepadId;
        public readonly HidDeviceRepresentation HidDevice;
        public readonly GamepadMapping Mapping;
        

        public GamepadConfiguration(uint gamepadId, string mappingJsonRepresentation, HidDeviceRepresentation hidDevice)
        {
            GamepadId = gamepadId;
            HidDevice = hidDevice;
            var loadedMapping = JsonConvert.DeserializeObject<GamepadMapping>(mappingJsonRepresentation);
            Mapping = loadedMapping;
        }        
    }
}