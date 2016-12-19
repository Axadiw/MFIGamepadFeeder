using System;
using System.Linq;
using System.Threading;
using HidSharp;
using MFIGamepadShared.Configuration;
using vGenWrapper;

public delegate void ErrorOccuredEventHandler(object sender, string errorMessage);

namespace MFIGamepadFeeder
{
    public class Gamepad: IDisposable
    {
        private readonly GamepadConfiguration _config;
        private readonly HidDeviceLoader _hidDeviceLoader;
        private readonly VGenWrapper _vGenWrapper;
        private Thread _gamepadUpdateThread;

        public Gamepad(GamepadConfiguration config, VGenWrapper vGenWrapper, HidDeviceLoader hidDeviceLoader)
        {
            _config = config;
            _vGenWrapper = vGenWrapper;
            _hidDeviceLoader = hidDeviceLoader;
        }        

        public void Dispose()
        {
            Stop();
        }

        public event ErrorOccuredEventHandler ErrorOccuredEvent;

        public bool Start()
        {
            return PlugInToXBoxController() && PlugInToHidDeviceAndStartLoop();
        }

        public void Stop()
        {
            UnPlugXBoxController();
            _gamepadUpdateThread?.Abort();
        }

        private bool UnPlugXBoxController()
        {
            var controllerPluggedIn = false;
            var checkIfPluggedIn = _vGenWrapper.vbox_isControllerPluggedIn(_config.GamepadId, ref controllerPluggedIn);

            if (checkIfPluggedIn != NtStatus.Success)
            {
                Log($"Failed to check if controller plugged in {_config.GamepadId} ({checkIfPluggedIn})!");
                return false;
            }

            if (controllerPluggedIn)
            {
                var unplugStatus = _vGenWrapper.vbox_UnPlug(_config.GamepadId);
                if (unplugStatus != NtStatus.Success)
                {
                    var forceUnplugStatus = _vGenWrapper.vbox_ForceUnPlug(_config.GamepadId);
                    if (forceUnplugStatus != NtStatus.Success)
                    {
                        Log($"Failed to force unplug gamepad {_config.GamepadId} ({forceUnplugStatus})!");
                        return false;
                    }                    
                }

                Log($"Successfully unplugged gamepad {_config.GamepadId}");
            }

            return true;
        }

        private bool PlugInToXBoxController()
        {
            if (!UnPlugXBoxController())
            {
                return false;
            }

            var plugInStatus = _vGenWrapper.vbox_PlugIn(_config.GamepadId);
            if (plugInStatus != NtStatus.Success)
            {
                Log($"Failed to plug in gamepad {_config.GamepadId} ({plugInStatus})!");
                return false;
            }

            _vGenWrapper.vbox_ResetController(_config.GamepadId);
            return true;
        }

        private void Log(string message)
        {
            ErrorOccuredEvent?.Invoke(this, message);
        }

        private bool PlugInToHidDeviceAndStartLoop()
        {
            var device =
                    _hidDeviceLoader.GetDevices(
                        _config.HidDevice.VendorId,
                        _config.HidDevice.ProductId,
                        _config.HidDevice.ProductVersion,
                        _config.HidDevice.SerialNumber
                    ).First();


            if (device == null)
            {
                Log(@"Failed to open device.");
                return false;
            }

            HidStream stream;
            if (!device.TryOpen(out stream))
            {
                Log("Failed to open device.");
                return false;
            }

            Log($"Successfully initialized gamepad {_config.GamepadId}");

            _gamepadUpdateThread?.Abort();
            _gamepadUpdateThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    using (stream)
                    {
                        while (true)
                        {
                            if (!Thread.CurrentThread.IsAlive)
                            {
                                break;
                            }

                            var bytes = new byte[device.MaxInputReportLength];
                            int count;
                            try
                            {
                                count = stream.Read(bytes, 0, bytes.Length);
                            }
                            catch (TimeoutException)
                            {
                                continue;
                            }
                            catch (Exception ex)
                            {
                                Log(ex.Message);
                                break;
                            }

                            if (count > 0)
                            {
                                UpdateState(bytes);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                }
            });
            _gamepadUpdateThread.Start();

            return true;
        }

        public void UpdateState(byte[] state)
        {
//            Log(string.Join(" ", state));

            XInputGamepadButtons buttonsState = 0;
            XInputGamepadDPadButtons dPadState = 0;

            for (var i = 0; i < _config.MappingItems.Count; i++)
            {
                var configForCurrentItem = _config.MappingItems[i];
                var itemValue = state[i];

                if (configForCurrentItem.Type == GamepadMappingItemType.Axis)
                {
                    UpdateAxis(itemValue, configForCurrentItem);
                }
                else if ((configForCurrentItem.Type == GamepadMappingItemType.DPad) && ConvertToButtonState(itemValue) &&
                         (configForCurrentItem.DPadType != null))
                {
                    dPadState |= configForCurrentItem.DPadType.Value;
                }
                else if ((configForCurrentItem.Type == GamepadMappingItemType.Button) && ConvertToButtonState(itemValue) &&
                         (configForCurrentItem.ButtonType != null))
                {
                    buttonsState |= configForCurrentItem.ButtonType.Value;
                }
            }

            var buttonPressState = _vGenWrapper.vbox_SetButton(_config.GamepadId, buttonsState, true);
            var buttonReleaseState = _vGenWrapper.vbox_SetButton(_config.GamepadId, ~buttonsState, false);
            var dPadStatus = _vGenWrapper.vbox_SetDpad(_config.GamepadId, dPadState);

            if (dPadStatus != NtStatus.Success)
            {
                Log($"Failed to set DPad {dPadStatus} (${dPadStatus}). Gamepad {_config.GamepadId}");
            }
            if (buttonPressState != NtStatus.Success)
            {
                Log($"Failed to set buttons (Press) {buttonsState} (${buttonPressState}). Gamepad {_config.GamepadId}");
            }
            if (buttonReleaseState != NtStatus.Success)
            {
                Log($"Failed to set buttons (Release) {~buttonsState} (${buttonReleaseState}). Gamepad {_config.GamepadId}");
            }
        }

        private void UpdateAxis(double itemValue, GamepadMappingItem configForCurrentItem)
        {
            var value = NormalizeAxis(itemValue, configForCurrentItem.ConvertAxis ?? false);

            if (configForCurrentItem.InvertAxis ?? false)
            {
                value = InvertNormalizedAxis(value);
            }

            var axisSetStatus = NtStatus.Success;
            switch (configForCurrentItem.AxisType)
            {
                case AxisType.Rx:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisRx(_config.GamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.Ry:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisRy(_config.GamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.Lx:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisLx(_config.GamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.Ly:
                    axisSetStatus = _vGenWrapper.vbox_SetAxisLy(_config.GamepadId, (short) (value*short.MaxValue));
                    break;
                case AxisType.LTrigger:
                    axisSetStatus = _vGenWrapper.vbox_SetTriggerL(_config.GamepadId, (byte) (value*byte.MaxValue));
                    break;
                case AxisType.RTrigger:
                    axisSetStatus = _vGenWrapper.vbox_SetTriggerR(_config.GamepadId, (byte) (value*byte.MaxValue));
                    break;
            }

            if (axisSetStatus != NtStatus.Success)
            {
                Log($"Failed to set axis {configForCurrentItem.AxisType} (${axisSetStatus}). Gamepad {_config.GamepadId}");
            }
        }

        private static double NormalizeAxis(double valueToNormalize, bool shouldConvert)
        {
            if (!shouldConvert)
            {
                return valueToNormalize/byte.MaxValue;
            }
            if (valueToNormalize < byte.MaxValue/2.0)
            {
                return valueToNormalize/(byte.MaxValue/2.0);
            }
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