using System.Collections.ObjectModel;
using MFIGamepadShared.Configuration;
using vGenWrapper;

namespace MFIGamepadConfigCreator
{
    internal class ConfigCreator
    {
        public GamepadConfiguration GetNimbusConfiguration()
        {
            var configItems = new Collection<GamepadConfigurationItem>
            {
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Empty
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadUp
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadRight
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadDown
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadLeft
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.A
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.B
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.X
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.Y
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.LBumper
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.RBumper
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Axis,
                    AxisType = AxisType.LTrigger
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Axis,
                    AxisType = AxisType.RTrigger
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Button,
                    ButtonType = XInputGamepadButtons.Start
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Axis,
                    AxisType = AxisType.Lx,
                    InvertAxis = false,
                    ConvertAxis = true
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Axis,
                    AxisType = AxisType.Ly,
                    InvertAxis = true,
                    ConvertAxis = true
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Axis,
                    AxisType = AxisType.Rx,
                    InvertAxis = false,
                    ConvertAxis = true
                },
                new GamepadConfigurationItem
                {
                    Type = GamepadItemType.Axis,
                    AxisType = AxisType.Ry,
                    InvertAxis = true,
                    ConvertAxis = true
                }
            };

            return new GamepadConfiguration {ConfigItems = configItems};
        }
    }
}