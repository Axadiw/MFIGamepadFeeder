﻿using System.Collections.Generic;
using MFIGamepadShared.Configuration;
using vGenWrapper;

namespace MFIGamepadConfigCreator
{
    internal class ConfigCreator
    {
        /* Creates a configuration as to the device specific 
         * stream bytes configuration (XInputGamepadButtons)
         */
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
                    ButtonType = XInputGamepadButtons.DpadUp
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    ButtonType = XInputGamepadButtons.DpadRight
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    ButtonType = XInputGamepadButtons.DpadDown
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    ButtonType = XInputGamepadButtons.DpadLeft
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
                    InvertAxis = false,
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
                    InvertAxis = false,
                    ConvertAxis = true
                }
            }, new List<VirtualKeyMappingItem>()
            {
                new VirtualKeyMappingItem()
                {
                    SourceKeys = new List<XInputGamepadButtons?>()
                    {
                        XInputGamepadButtons.RTrigger,
                        XInputGamepadButtons.LTrigger,
                        XInputGamepadButtons.Start
                    },
                    DestinationItem = XInputGamepadButtons.Back
                },
                new VirtualKeyMappingItem()
                {
                    SourceKeys = new List<XInputGamepadButtons?>()
                    {
                        XInputGamepadButtons.LTrigger,
                        XInputGamepadButtons.Start
                    },
                    DestinationItem = XInputGamepadButtons.LeftStick
                },
                new VirtualKeyMappingItem()
                {
                    SourceKeys = new List<XInputGamepadButtons?>()
                    {
                        XInputGamepadButtons.RTrigger,
                        XInputGamepadButtons.Start
                    },
                    DestinationItem = XInputGamepadButtons.RightStick
                }
            });
        }

        public GamepadMapping GetMiniConfiguration()
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
                    ButtonType = XInputGamepadButtons.DpadUp
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    ButtonType = XInputGamepadButtons.DpadRight
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    ButtonType = XInputGamepadButtons.DpadDown
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.DPad,
                    ButtonType = XInputGamepadButtons.DpadLeft
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
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.LTrigger
                },
                new GamepadMappingItem
                {
                    Type = GamepadMappingItemType.Button,
                    ButtonType = XInputGamepadButtons.RTrigger
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
                    InvertAxis = false,
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
                    InvertAxis = false,
                    ConvertAxis = true
                }
            }, new List<VirtualKeyMappingItem>()
            {
                new VirtualKeyMappingItem()
                {
                    SourceKeys = new List<XInputGamepadButtons?>()
                    {
                        XInputGamepadButtons.RTrigger,
                        XInputGamepadButtons.LTrigger,
                        XInputGamepadButtons.Start
                    },
                    DestinationItem = XInputGamepadButtons.Back
                },
                new VirtualKeyMappingItem()
                {
                    SourceKeys = new List<XInputGamepadButtons?>()
                    {
                        XInputGamepadButtons.LTrigger,
                        XInputGamepadButtons.Start
                    },
                    DestinationItem = XInputGamepadButtons.LeftStick
                },
                new VirtualKeyMappingItem()
                {
                    SourceKeys = new List<XInputGamepadButtons?>()
                    {
                        XInputGamepadButtons.RTrigger,
                        XInputGamepadButtons.Start
                    },
                    DestinationItem = XInputGamepadButtons.RightStick
                }
            });
        }
    }
}