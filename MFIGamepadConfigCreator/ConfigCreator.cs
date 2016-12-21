using System.Collections.Generic;
using MFIGamepadShared.Configuration;
using vGenWrapper;

namespace MFIGamepadConfigCreator
{
    internal class ConfigCreator
    {
        public GamepadMapping GetNimbusConfiguration()
        {
            return new GamepadMapping(new List<GamepadMappingItem>
            {
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Empty
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadUp
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadRight
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadDown
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    DPadType = XInputGamepadDPadButtons.DpadLeft
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.A
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.B
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.X
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.Y
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.LBumper
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.RBumper
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Axis,
                    AxisType = AxisType.LTrigger
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Axis,
                    AxisType = AxisType.RTrigger
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.Start
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Axis,
                    AxisType = AxisType.Lx,
                    InvertAxis = false,
                    ConvertAxis = true
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Axis,
                    AxisType = AxisType.Ly,
                    InvertAxis = true,
                    ConvertAxis = true
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Axis,
                    AxisType = AxisType.Rx,
                    InvertAxis = false,
                    ConvertAxis = true
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Axis,
                    AxisType = AxisType.Ry,
                    InvertAxis = true,
                    ConvertAxis = true
                }
            });
        }
    }
}