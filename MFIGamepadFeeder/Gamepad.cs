using System;
using System.Collections.ObjectModel;
using vJoyInterfaceWrap;

namespace MFIGamepadFeeder
{
    internal class Gamepad
    {
        private readonly int _inputItemsCount;
        private readonly long _maxval;
        private readonly vJoy _vJoy;
        private readonly uint _gamepadId;

        public Gamepad(int inputItemsCount)
        {
            _inputItemsCount = inputItemsCount;
            // Create one _vJoy object and a position structure.
            _vJoy = new vJoy();
            _gamepadId = 1;

            // Get the driver attributes (Vendor ID, Product ID, Version Number)
            if (!_vJoy.vJoyEnabled())
            {
                Console.WriteLine("vJoy driver not enabled: Failed Getting vJoy attributes.\n");
                return;
            }
            Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n", _vJoy.GetvJoyManufacturerString(),
                _vJoy.GetvJoyProductString(), _vJoy.GetvJoySerialNumberString());

            // Get the state of the requested device
            var status = _vJoy.GetVJDStatus(_gamepadId);
            switch (status)
            {
                case VjdStat.VJD_STAT_OWN:
                    Console.WriteLine("vJoy Device {0} is already owned by this feeder\n", _gamepadId);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    Console.WriteLine("vJoy Device {0} is free\n", _gamepadId);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    Console.WriteLine("vJoy Device {0} is already owned by another feeder\nCannot continue\n",
                        _gamepadId);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    Console.WriteLine("vJoy Device {0} is not installed or disabled\nCannot continue\n", _gamepadId);
                    return;
                default:
                    Console.WriteLine("vJoy Device {0} general error\nCannot continue\n", _gamepadId);
                    return;
            }


            // Test if DLL matches the driver
            uint DllVer = 0, DrvVer = 0;
            var match = _vJoy.DriverMatch(ref DllVer, ref DrvVer);
            if (match)
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})\n", DllVer);
            else
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})\n", DrvVer, DllVer);


            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && !_vJoy.AcquireVJD(_gamepadId)))
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.\n", _gamepadId);
                return;
            }

            ResetGamepad(_gamepadId);
        }

        private void ResetGamepad(uint id)
        {
            _vJoy.ResetVJD(id);
            var zeroState = new byte[_inputItemsCount];

            for (var i = 0; i < _inputItemsCount; i++)
            {
                zeroState[i] = 0;
            }

            Gamepad_NewState(zeroState);
        }

        public void Gamepad_NewState(byte[] state)
        {
            for (var i = 0; i < 18; i++)
            {
                Console.Write("{0} ", (int) state[i]);
            }
            Console.WriteLine();

            var gamepadConfigurationItems = GetConfiguration();
            for (var i = 0; i < gamepadConfigurationItems.Count; i++)
            {
                SetGamepadItem(state, i, gamepadConfigurationItems[i]);
            }

            setDPad(state, gamepadConfigurationItems);
        }

        private void SetGamepadItem(byte[] values, int index, GamepadConfigurationItem config)
        {
            double value = values[index];
            if (config.Type == GamepadItemType.Axis)
            {
                long maxAxisValue = 0;
                var targetAxis = config.TargetUsage ?? HID_USAGES.HID_USAGE_X;
                _vJoy.GetVJDAxisMax(_gamepadId, targetAxis, ref maxAxisValue);
                value = NormalizeAxis((byte) value, config.ConvertAxis ?? false);

                if (config.InvertAxis ?? false)
                {
                    value = InvertNormalizedAxis(value);
                }

                _vJoy.SetAxis((int) (value*maxAxisValue), _gamepadId, targetAxis);
            }
            else if (config.Type == GamepadItemType.Button)
            {
                _vJoy.SetBtn(ConvertToButtonState((byte) value), _gamepadId, config.TargetButtonId ?? 0);
            }
        }

        private void setDPad(byte[] values, Collection<GamepadConfigurationItem> config)
        {
            var dPadUp = false;
            var dPadRight = false;
            var dPadDown = false;
            var dPadLeft = false;

            for (var i = 0; i < config.Count; i++)
            {
                if (config[i].Type == GamepadItemType.DPadUp)
                {
                    dPadUp = ConvertToButtonState(values[i]);
                }

                if (config[i].Type == GamepadItemType.DPadRight)
                {
                    dPadRight = ConvertToButtonState(values[i]);
                }

                if (config[i].Type == GamepadItemType.DPadDown)
                {
                    dPadDown = ConvertToButtonState(values[i]);
                }

                if (config[i].Type == GamepadItemType.DPadLeft)
                {
                    dPadLeft = ConvertToButtonState(values[i]);
                }
            }

            var angle = -1;

            if (dPadUp && dPadRight)
            {
                angle = 4500;
            }
            else if (dPadRight && dPadDown)
            {
                angle = 13500;
            }
            else if (dPadDown && dPadLeft)
            {
                angle = 22500;
            }
            else if (dPadLeft && dPadUp)
            {
                angle = 31500;
            }
            else if (dPadUp)
            {
                angle = 0;
            }
            else if (dPadRight)
            {
                angle = 9000;
            }
            else if (dPadDown)
            {
                angle = 18000;
            }
            else if (dPadLeft)
            {
                angle = 27000;
            }


            _vJoy.SetContPov(angle, _gamepadId, 1);
        }


        private double NormalizeAxis(byte valueToNormalize, bool shouldConvert)
        {
            if (shouldConvert)
            {
                if (valueToNormalize < byte.MaxValue/2.0)
                {
                    return (valueToNormalize + byte.MaxValue/2.0)/byte.MaxValue;
                }
                return (valueToNormalize - byte.MaxValue/2.0)/byte.MaxValue;
            }

            return (double) valueToNormalize/byte.MaxValue;
        }

        private double InvertNormalizedAxis(double axisToInvert)
        {
            return 1.0 - axisToInvert;
        }

        private bool ConvertToButtonState(byte value)
        {
            return value > 0;
        }

        private Collection<GamepadConfigurationItem> GetConfiguration()
        {
            var returnItems = new Collection<GamepadConfigurationItem>
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
                    TargetUsage = HID_USAGES.HID_USAGE_SL0,
                    InvertAxis = false,
                    ConvertAxis = false,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = HID_USAGES.HID_USAGE_SL1,
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
                    TargetUsage = HID_USAGES.HID_USAGE_X,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = HID_USAGES.HID_USAGE_Y,
                    InvertAxis = true,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = HID_USAGES.HID_USAGE_RX,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = HID_USAGES.HID_USAGE_RY,
                    InvertAxis = true,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                }
            };

            return returnItems;
        }
    }
}