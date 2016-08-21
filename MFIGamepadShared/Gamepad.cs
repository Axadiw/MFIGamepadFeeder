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
        /**vJoyInterface wrapper*/
        private readonly IWrapper _vBox;
        public bool AddRTLT = false, AddBack = false;
        /**
         * Gamepad IWrapper is created if the VBus exists on the host.
         * **/
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
            /** The user may change gamepad ID to a "FREE" VJD ID */
            if ((status == VJD_STAT_BUSY || status == VJD_STAT_OWN) || ((status == VJD_STAT_FREE) && !_vBox.AcquireVJD(_gamepadId)))
            {
                string msg = "?";
                if (status == VJD_STAT_OWN)
                    msg = "OWN";
                if (status == VJD_STAT_BUSY)
                    msg = "BUSY";
                Log($@"Failed to acquire vJoy device number {_gamepadId}. {status}:{msg}");
                return;
            }
        }

        /** plug Xbox feeder device*/
        public Boolean plug()
        {
            // XboxInterface plugin
            if (!_vBox.PlugIn(_gamepadId))
            {
                Log($@"Failed to plugIn vJoy device number {_gamepadId}.");
                return false;
            }
            // reset State
            ResetGamepad(_gamepadId);
            return true;
        }

        /**unplug Xbox feeder device
         Called by the application when exiting */
        public Boolean unPlug(Boolean force)
        {
            if (force)
                return _vBox.UnPlugForce(_gamepadId);
            else
                return _vBox.UnPlug(_gamepadId);
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

        public unsafe void UpdateState(byte[] state)
        {
            //            Log(string.Join(" ", state));
            uint dPad = 0, btns = 0, btnsOff = 0;
            short AxisX = 0, AxisY = 0, AxisRX = 0, AxisRY = 0;
            byte AxisSL0 = 0x00, AxisSL1 = 0x00;
            for (var i = 0; i < _config.ConfigItems.Count; i++)
            {
                GetAxis(state, i, &AxisX, &AxisY, &AxisRX, &AxisRY, &AxisSL0, &AxisSL1);
                /** sent directly to vJoy*/
                GetGamepadItem(state, i, &btns, &btnsOff);
                GetDpad(state, i, &dPad);
            }

            /** Send data from Btns, Axes and Triggers to vJoy*/
            addBtnHacks(AxisSL0, AxisSL1, &btns, &btnsOff);
            _vBox.SetBtnAny(_gamepadId, false, btnsOff);
            _vBox.SetBtnAny(_gamepadId, true, btns);
            int maxAxisValue = 0;
            _vBox.GetVJDAxisMax(_gamepadId, hid_X, &maxAxisValue);
            _vBox.SetAxisXY(_gamepadId, AxisX, AxisY, hid_X, hid_Y, xinput_LAXIS_DEADZONE, (short)maxAxisValue);
            _vBox.GetVJDAxisMax(_gamepadId, hid_X, &maxAxisValue);
            _vBox.SetAxisXY(_gamepadId, AxisRX, AxisRY, hid_RX, hid_RY, xinput_RAXIS_DEADZONE, (short)maxAxisValue);
            _vBox.SetTriggerLR(_gamepadId, AxisSL0, AxisSL1, xinput_TRIGGER_THRESHOLD);
            _vBox.SetDpad(_gamepadId, dPad);
            // debug read values
#if DEBUG
            _vBox.GetAxisXY(_gamepadId, &AxisX, &AxisY, hid_X, hid_Y);
            _vBox.GetAxisXY(_gamepadId, &AxisRX, &AxisRY, hid_RX, hid_RY);
            _vBox.GetTriggerLR(_gamepadId, &AxisSL0, &AxisSL1);
#endif
        }
        /** BACK, LT/RT hacks. */
        private unsafe void addBtnHacks(byte AxisSL0, byte AxisSL1, uint* btns, uint* btnsOff)
        {
            /** init "off" buttons*/
            *btnsOff |= xinput_GAMEPAD_BACK | xinput_GAMEPAD_LEFT_THUMB | xinput_GAMEPAD_RIGHT_THUMB;
            /** Hacks on the START button. It's mapped to the MENU button on the Nimbus controller.*/
            if (0 != (*btns & xinput_GAMEPAD_START) && (AddBack || AddRTLT))
            {
                /**
                 * At first, START+L2/R2 are pressed together...
                 * Don't compute with any Deadzone, 
                 * that allows a "light" usage of the trigger and combine with the START btn
                 */
                if (AxisSL0 > 0 || AxisSL1 > 0)
                {
                    /**disable START btn */
                    *btns &= ~xinput_GAMEPAD_START;
                    *btnsOff |= xinput_GAMEPAD_START;
                    
                    /** Simulate a back button.
                     * button start is the back button if LTrigger+RTrigger+START are pressed simultaneously */
                    if (AddBack)
                    {
                        if (AxisSL0 > 0 && AxisSL1 > 0)
                        {
                            *btns |= xinput_GAMEPAD_BACK;
                            *btnsOff  &= ~xinput_GAMEPAD_BACK;
                        }
                    }
                    /** The MFi Nimbus controller doesn't provide left and right Thumb buttons 
                     * so we can simulate and add a btn press (LTrigger or RTrigger+START)
                     */
                    if (AddRTLT)
                    {
                        if (AxisSL0 > 0)
                        {
                            *btns |= xinput_GAMEPAD_LEFT_THUMB;
                            *btnsOff &= ~xinput_GAMEPAD_LEFT_THUMB;
                        }
                        else if (AxisSL1 > 0)
                        {
                            *btns |= xinput_GAMEPAD_RIGHT_THUMB;
                            *btnsOff &= ~xinput_GAMEPAD_RIGHT_THUMB;
                        }
                    }
                }
            }
        }
        /**
         A normalized axis value is sent to vJoy Driver.
            */
        private unsafe void GetAxis(byte[] values, int index, short* AxisX, short* AxisY,
            short* AxisRX, short* AxisRY, byte* AxisSL0, byte* AxisSL1)
        {
            float value = values[index];
            /** obtained from .mficonfiguration files*/
            GamepadConfigurationItem config = _config.ConfigItems[index];
            if (config.Type == GamepadItemType.Axis)
            {
                int maxAxisValue = 0, minAxisValue = 0;
                uint targetAxis = config.TargetUsage ?? 0;
                if (targetAxis == 0) return;
                /** We need to know the maximum value for the axis (trigger/Slider is 255 and left/right 
                analog sticks +-32767 **/
                _vBox.GetVJDAxisMax(_gamepadId, targetAxis, &maxAxisValue);
                _vBox.GetVJDAxisMin(_gamepadId, targetAxis, &minAxisValue);
                /** MFI controller returns [0;Byte.MaxValue] axes, normalize [0;1]*/
                value = NormalizeAxis((byte)value, config.ConvertAxis ?? false);

                if (config.InvertAxis ?? false)
                {
                    value = InvertNormalizedAxis(value);
                }
                /** define byte value with maximum Axis value from xInput*/
                value = (value * (maxAxisValue - minAxisValue) + minAxisValue);
                if (targetAxis == hid_X)
                {
                    *AxisX = (short)value;
                }
                else if (targetAxis == hid_Y)
                {
                    *AxisY = (short)value;
                }
                else if (targetAxis == hid_RX)
                {
                    *AxisRX = (short)value;
                }
                else if (targetAxis == hid_RY)
                {
                    *AxisRY = (short)value;
                }
                else if (targetAxis == hid_SL0)
                {
                    *AxisSL0 = (byte)value;
                }
                else if (targetAxis == hid_SL1)
                {
                    *AxisSL1 = (byte)value;
                }
            }
        }
        private unsafe void GetGamepadItem(byte[] values, int index, uint* btns, uint* btnsOff)
        {
            double value = values[index];
            GamepadConfigurationItem config = _config.ConfigItems[index];
            if (config.Type == GamepadItemType.Button)
            {
                if (ConvertToButtonState((byte)value))
                    *btns |= config.TargetButtonId ?? 0;
                else
                    *btnsOff |= config.TargetButtonId ?? 0;
            }
        }
        /** dPad flags*/
        /** concatenate dPad values */
        private unsafe void GetDpad(byte[] state, int index, uint* dPad)
        {
            bool buttonState = ConvertToButtonState((byte)state[index]);
            if (buttonState)
            {
                GamepadConfigurationItem config = _config.ConfigItems[index];
                if (config.Type == GamepadItemType.DPadUp)
                {
                    *dPad |= xinput_DPAD_UP;
                }

                if (config.Type == GamepadItemType.DPadRight)
                {
                    *dPad |= xinput_DPAD_RIGHT;
                }

                if (config.Type == GamepadItemType.DPadDown)
                {
                    *dPad |= xinput_DPAD_DOWN;
                }

                if (config.Type == GamepadItemType.DPadLeft)
                {
                    *dPad |= xinput_DPAD_LEFT;
                }
            }
        }

        /**
         * Always normalize to Byte, before to compute vJoy values.
         * param name="valueToNormalize" unsigned 8-bit integer sent by the MFI controller
         * param param name="shouldConvert" decodes MFi uint ... byte.MaxValue/2.0 is the center 0.
         * returns is a double between [0 ; 1.0]
         **/
        private float NormalizeAxis(byte valueToNormalize, bool shouldConvert)
        {
            if (shouldConvert) // Needed by all continuous inputs (axes and triggers)
            {
                if (valueToNormalize < byte.MaxValue / 2.0f)
                {
                    return (valueToNormalize + byte.MaxValue / 2.0f) / byte.MaxValue;
                }
                return (valueToNormalize - byte.MaxValue / 2.0f) / byte.MaxValue;
            }

            return (float)valueToNormalize / byte.MaxValue;
        }

        /** invert +/- axis value **/
        private float InvertNormalizedAxis(float axisToInvert)
        {
            return 1.0f - axisToInvert;
        }

        /** 
         * returns true if > 0
         * **/
        private bool ConvertToButtonState(byte value)
        {
            return value > 0;
        }
    }
}