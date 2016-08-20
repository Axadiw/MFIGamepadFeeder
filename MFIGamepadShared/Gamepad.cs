using System;
using System.Collections.ObjectModel;
using System.Linq;
using MFIGamepadFeeder.Gamepads.Configuration;
using MFIGamepadShared.Configuration;
using vXbox;
using static vXbox.IWrapper;

public delegate void ErorOccuredEventHandler(object sender, string errorMessage);

namespace MFIGamepadFeeder
{
    public class Gamepad
    {
        private readonly GamepadConfiguration _config;
        private readonly uint _gamepadId;
        private readonly IWrapper _vBox;

        public Gamepad(GamepadConfiguration config, uint gamepadId)
        {
            _config = config;
            _vBox = new IWrapper();
            _gamepadId = gamepadId;

            // Test if bus exists
            bool bus = _vBox.isVBusExists();
            if (bus)
                Log(@"Virtual Xbox bus exists\n\n");
            else
            {
                Log(@"Virtual Xbox bus does NOT exist - Aborting\n\n");
                return;
            }


            //uint dllVer = 0, drvVer = 0;
            //var match = _vBox.DriverMatch(ref dllVer, ref drvVer);
            //if (!match)
            //{
            //    Log($@"Version of Driver ({drvVer:X}) does NOT match DLL Version ({dllVer:X})\n");
            //    return;
            //}

            var status = _vBox.GetVJDStatus(_gamepadId);
            if ((status == VJD_STAT_OWN) || ((status == VJD_STAT_FREE) && !_vBox.AcquireVJD(_gamepadId)))
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
            _vBox.ResetVJD(id);
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

        private unsafe void SetGamepadItem(byte[] values, int index, GamepadConfigurationItem config)
        {
            double value = values[index];
            if (config.Type == GamepadItemType.Axis)
            {
                int maxAxisValue = 0;
                var targetAxis = config.TargetUsage ?? _vBox.hid_X;
                _vBox.GetVJDAxisMax(_gamepadId, targetAxis, &maxAxisValue);
                value = NormalizeAxis((byte) value, config.ConvertAxis ?? false);

                if (config.InvertAxis ?? false)
                {
                    value = InvertNormalizedAxis(value);
                }

                _vBox.SetAxis((int) (value*maxAxisValue), _gamepadId, targetAxis);
            }
            else if (config.Type == GamepadItemType.Button)
            {
                _vBox.SetBtn(ConvertToButtonState((byte) value), _gamepadId, (byte)(config.TargetButtonId ?? 0));
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


            if (dPadUp && dPadRight)
            {
                _vBox.SetDpadUpRight(_gamepadId);
            }
            else if (dPadRight && dPadDown)
            {
                _vBox.SetDpadDownRight(_gamepadId);
            }
            else if (dPadDown && dPadLeft)
            {
                _vBox.SetDpadDownLeft(_gamepadId);
            }
            else if (dPadLeft && dPadUp)
            {
                _vBox.SetDpadUpLeft(_gamepadId);
            }
            else if (dPadUp)
            {
                _vBox.SetDpadUp(_gamepadId);
            }
            else if (dPadRight)
            {
                _vBox.SetDpadRight(_gamepadId);
            }
            else if (dPadDown)
            {
                _vBox.SetDpadDown(_gamepadId);
            }
            else if (dPadLeft)
            {
                _vBox.SetDpadLeft(_gamepadId);
            }
            _vBox.SetDpadOff(_gamepadId);
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