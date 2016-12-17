using MFIGamepadShared.Configuration;
using vGenWrapper;

public delegate void ErrorOccuredEventHandler(object sender, string errorMessage);

namespace MFIGamepadFeeder
{
    public class Gamepad
    {
        private readonly GamepadConfiguration _config;
        private readonly uint _gamepadId;
        private readonly VGenWrapper _vGenWrapper;

        public Gamepad(GamepadConfiguration config, uint gamepadId, VGenWrapper vGenWrapper,
            ErrorOccuredEventHandler gamepadErrorOccuredEvent)
        {
            ErrorOccuredEvent += gamepadErrorOccuredEvent;
            _config = config;
            _vGenWrapper = vGenWrapper;
            _gamepadId = gamepadId;

            var controllerPluggedIn = false;
            var checkIfPluggedIn = vGenWrapper.vbox_isControllerPluggedIn(_gamepadId, ref controllerPluggedIn);

            if (checkIfPluggedIn != NtStatus.Success)
            {
                Log($"Failed to check if controller plugged in {_gamepadId} ({checkIfPluggedIn})!");
                return;
            }

            if (controllerPluggedIn)
            {
                var unplugStatus = vGenWrapper.vbox_UnPlug(_gamepadId);
                if (unplugStatus != NtStatus.Success)
                {
                    var forceUnplugStatus = vGenWrapper.vbox_ForceUnPlug(_gamepadId);
                    if (forceUnplugStatus != NtStatus.Success)
                    {
                        Log($"Failed to force unplug gamepad {_gamepadId} ({forceUnplugStatus})!");
                        return;
                    }
                }
            }

            var plugInStatus = vGenWrapper.vbox_PlugIn(_gamepadId);
            if (plugInStatus != NtStatus.Success)
            {
                Log($"Failed to plug in gamepad {_gamepadId} ({plugInStatus})!");
                return;
            }

            vGenWrapper.vbox_ResetController(_gamepadId);
        }

        public event ErrorOccuredEventHandler ErrorOccuredEvent;

        private void Log(string message)
        {
            ErrorOccuredEvent?.Invoke(this, message);
        }

        public void UpdateState(byte[] state)
        {
//            Log(string.Join(" ", state));

            XInputGamepadButtons buttonsState = 0;
            XInputGamepadDPadButtons dPadState = 0;

            for (var i = 0; i < _config.ConfigItems.Count; i++)
            {
                var configForCurrentItem = _config.ConfigItems[i];
                var itemValue = state[i];

                if (configForCurrentItem.Type == GamepadItemType.Axis)
                    UpdateAxis(itemValue, configForCurrentItem);
                else if ((configForCurrentItem.Type == GamepadItemType.DPad) && ConvertToButtonState(itemValue) &&
                         (configForCurrentItem.DPadType != null))
                    dPadState |= configForCurrentItem.DPadType.Value;
                else if ((configForCurrentItem.Type == GamepadItemType.Button) && ConvertToButtonState(itemValue) &&
                         (configForCurrentItem.ButtonType != null))
                    buttonsState |= configForCurrentItem.ButtonType.Value;
            }

            var buttonPressState = _vGenWrapper.vbox_SetButton(_gamepadId, buttonsState, true);
            var buttonReleaseState = _vGenWrapper.vbox_SetButton(_gamepadId, ~buttonsState, false);
            var dPadStatus = _vGenWrapper.vbox_SetDpad(_gamepadId, dPadState);

            if (dPadStatus != NtStatus.Success)
                Log($"Failed to set DPad {dPadStatus} (${dPadStatus})");
            if (buttonPressState != NtStatus.Success)
                Log($"Failed to set buttons (Press) {buttonsState} (${buttonPressState})");
            if (buttonReleaseState != NtStatus.Success)
                Log($"Failed to set buttons (Release) {~buttonsState} (${buttonReleaseState})");
        }

        private void UpdateAxis(double itemValue, GamepadConfigurationItem configForCurrentItem)
        {
            var value = NormalizeAxis(itemValue, configForCurrentItem.ConvertAxis ?? false);

            if (configForCurrentItem.InvertAxis ?? false)
                value = InvertNormalizedAxis(value);

            var axisSetStatus = NtStatus.Success;
            switch (configForCurrentItem.AxisType)
            {
                case AxisType.Rx:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisRx(_gamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.Ry:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisRy(_gamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.Lx:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisLx(_gamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.Ly:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisLy(_gamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.LTrigger:
                    axisSetStatus = _vGenWrapper.vbox_SetTriggerL(_gamepadId, (byte) (value*byte.MaxValue));
                    break;
                case AxisType.RTrigger:
                    axisSetStatus = _vGenWrapper.vbox_SetTriggerR(_gamepadId, (byte) (value*byte.MaxValue));
                    break;
            }

            if (axisSetStatus != NtStatus.Success)
                Log($"Failed to set axis {configForCurrentItem.AxisType} (${axisSetStatus})");
        }

        private static double NormalizeAxis(double valueToNormalize, bool shouldConvert)
        {
            if (!shouldConvert) return valueToNormalize/byte.MaxValue;
            if (valueToNormalize < byte.MaxValue/2.0)
                return valueToNormalize/(byte.MaxValue/2.0);
            return (valueToNormalize - byte.MaxValue)/(byte.MaxValue/2.0);
        }

        private static double InvertNormalizedAxis(double axisToInvert)
        {
            return 1.0 - axisToInvert;
        }

        private static bool ConvertToButtonState(byte value)
        {
            return value > 0;
        }
    }
}