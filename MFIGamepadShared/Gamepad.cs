using System;
using System.Collections.ObjectModel;
using System.Linq;
using MFIGamepadFeeder.Gamepads.Configuration;
using MFIGamepadShared.Configuration;
using vJoyInterfaceWrap;

public delegate void ErorOccuredEventHandler(object sender, string errorMessage);

namespace MFIGamepadFeeder
{
    public class Gamepad
    {
        private readonly GamepadConfiguration _config;
        private readonly uint _gamepadId;
        private readonly vJoy _vJoy;

        public Gamepad(GamepadConfiguration config, uint gamepadId)
        {
            _config = config;
            _vJoy = new vJoy();
            _gamepadId = gamepadId;

            if (!_vJoy.vJoyEnabled())
            {
                Log(@"vJoy driver not enabled: Failed Getting vJoy attributes.");
                return;
            }

            uint dllVer = 0, drvVer = 0;
            var match = _vJoy.DriverMatch(ref dllVer, ref drvVer);
            if (!match)
            {
                Log($@"Version of Driver ({drvVer:X}) does NOT match DLL Version ({dllVer:X})\n");
                return;
            }

            var status = _vJoy.GetVJDStatus(_gamepadId);
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && !_vJoy.AcquireVJD(_gamepadId)))
            {
                Log($@"Failed to acquire vJoy device number {_gamepadId}.\n");
                return;
            }

            ResetGamepad(_gamepadId);
        }

        public event ErorOccuredEventHandler ErrorOccuredEvent;

        private void Log(string message)
        {
            ErrorOccuredEvent?.Invoke(this, message);
        }

        private void ResetGamepad(uint id)
        {
            _vJoy.ResetVJD(id);
            var zeroState = new byte[_config.ConfigItems.Count];

            for (var i = 0; i < _config.ConfigItems.Count; i++)
            {
                zeroState[i] = 0;
            }

            UpdateState(zeroState);
        }

        public void UpdateState(byte[] state)
        {            
//            Log(string.Join(" ", state));

            for (var i = 0; i < _config.ConfigItems.Count; i++)
            {
                SetGamepadItem(state, i, _config.ConfigItems[i]);
            }

            SetDPad(state, _config.ConfigItems);
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

        private void SetDPad(byte[] values, Collection<GamepadConfigurationItem> config)
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
    }
}