using System.Collections.ObjectModel;
using MFIGamepadFeeder.Gamepads.Configuration;
using MFIGamepadShared.Configuration;
using vXbox;

namespace MFIGamepadConfigCreator
{
    internal class ConfigCreator
    {
        public GamepadConfiguration GetNimbusConfiguration()
        {
            var _vBox = new IWrapper();
            var configItems = new Collection<GamepadConfigurationItem>
            {
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.Empty
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadUp
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadRight
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadDown
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadLeft
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 1,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 2,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 3,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 4,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 5,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 6,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = _vBox.hid_SL0,
                    InvertAxis = false,
                    ConvertAxis = false,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = _vBox.hid_SL1,
                    InvertAxis = false,
                    ConvertAxis = false,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = 7,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = _vBox.hid_X,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = _vBox.hid_Y,
                    InvertAxis = true,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = _vBox.hid_RX,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = _vBox.hid_RY,
                    InvertAxis = true,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                }
            };

            return new GamepadConfiguration {ConfigItems = configItems};
        }
    }
}