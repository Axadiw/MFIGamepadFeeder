using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace MFIGamepadShared.Configuration
{
    public class GamepadConfiguration
    {
        public GamepadConfiguration()
        {
        }

        public GamepadConfiguration(string jsonRepresentation)
        {
            var loadedConfig = JsonConvert.DeserializeObject<GamepadConfiguration>(jsonRepresentation);
            ConfigItems = loadedConfig.ConfigItems;
        }

        public Collection<GamepadConfigurationItem> ConfigItems { get; set; }

        public string GetJsonRepresentation()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}