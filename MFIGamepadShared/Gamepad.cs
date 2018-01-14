using System;
using System.Collections.Generic;
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
        private Thread _gamepadAliveThread;
        private IDictionary<XInputGamepadButtons, XInputGamepadButtons> _virtualMappings;

        public Gamepad(GamepadConfiguration config, VGenWrapper vGenWrapper, HidDeviceLoader hidDeviceLoader)
        {
            _config = config;
            _vGenWrapper = vGenWrapper;
            _hidDeviceLoader = hidDeviceLoader;
            _virtualMappings = new Dictionary<XInputGamepadButtons, XInputGamepadButtons>();

            foreach (var virtualMapping in _config.Mapping.VirtualKeysItems.Where(item => item.DestinationItem != null))
            {
                var virtualPattern = virtualMapping.SourceKeys
                    .Where(sourceKey => sourceKey != null)
                    .Aggregate((XInputGamepadButtons)0, (current, sourceKey) => current | sourceKey.Value);

                // ReSharper disable once PossibleInvalidOperationException
                _virtualMappings[virtualPattern] = (XInputGamepadButtons) virtualMapping.DestinationItem;
            }
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

        private void _deviceStream(HidDevice device, HidStream stream)
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
                            UpdateState(bytes, 0, 0, 0, 0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
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
                _deviceStream(device, stream);
            });
            _gamepadUpdateThread.Start();
            _gamepadAliveThread?.Abort();
            _gamepadAliveThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (_gamepadUpdateThread.IsAlive)
                {
                    _gamepadUpdateThread.Join();
                    Log($"Lost connection. Waiting for the device...");
                    while (!device.TryOpen(out stream))
                    {
                        Thread.Sleep(1000);
                    }
                    _gamepadUpdateThread = new Thread(() =>
                    {
                        Log($"Stream was resumed.");
                        _deviceStream(device, stream);
                    });
                    _gamepadUpdateThread.Start();
                }
            });
            _gamepadAliveThread.Start();
            return true;
        }

        public void UpdateState(byte[] state, XInputGamepadButtons buttonsState, XInputGamepadButtons dPadState, XInputGamepadButtons triggerState, int i)
        {
            // UpdateState recurses state.count + 1 times (17)
            if (i < _config.Mapping.MappingItems.Count && i < state.Count())
            {
                var configForCurrentItem = _config.Mapping.MappingItems[i];
                var itemValue = state[i];
                
                if (configForCurrentItem.Type == GamepadMappingItemType.Axis)
                {
                    if (ConvertToButtonState(itemValue))
                        triggerState |= (XInputGamepadButtons)((int)XInputGamepadButtons.All & ((int)configForCurrentItem.AxisType.Value << 8));
                    UpdateAxis(itemValue, configForCurrentItem);
                }
                else if ((configForCurrentItem.Type == GamepadMappingItemType.DPad) && ConvertToButtonState(itemValue) &&
                         (configForCurrentItem.ButtonType != null))
                {
                    dPadState |= configForCurrentItem.ButtonType.Value;
                }
                else if ((configForCurrentItem.Type == GamepadMappingItemType.Button) && ConvertToButtonState(itemValue) &&
                         (configForCurrentItem.ButtonType != null))
                {
                    buttonsState |= configForCurrentItem.ButtonType.Value;
                }
                UpdateState(state, buttonsState, dPadState, triggerState, i + 1);
                return;
            }
            
            foreach (var virtualMapping in _virtualMappings)
            {
                if ((virtualMapping.Key & (buttonsState | dPadState | triggerState)) == virtualMapping.Key)
                {
                    buttonsState |= virtualMapping.Value;
                    buttonsState ^= virtualMapping.Key;
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
            axisSetStatus = SetVBoxStat(configForCurrentItem, value, axisSetStatus);
            if (axisSetStatus != NtStatus.Success)
            {
                Log($"Failed to set axis {configForCurrentItem.AxisType} (${axisSetStatus}). Gamepad {_config.GamepadId}");
            }
        }

        private NtStatus SetVBoxStat(GamepadMappingItem configForCurrentItem, double value, NtStatus axisSetStatus)
        {
            // NOTE : if else are faster than switch cases
            if (AxisType.Rx == configForCurrentItem.AxisType)
                axisSetStatus = _vGenWrapper.vbox_SetAxisRx(_config.GamepadId, (short)(value * short.MaxValue));
            else if (AxisType.Ry == configForCurrentItem.AxisType)
                axisSetStatus = _vGenWrapper.vbox_SetAxisRy(_config.GamepadId, (short)(value * short.MaxValue));
            else if (AxisType.Lx == configForCurrentItem.AxisType)
                axisSetStatus = _vGenWrapper.vbox_SetAxisLx(_config.GamepadId, (short)(value * short.MaxValue));
            else if (AxisType.Ly == configForCurrentItem.AxisType)
                axisSetStatus = _vGenWrapper.vbox_SetAxisLy(_config.GamepadId, (short)(value * short.MaxValue));
            else if (AxisType.LTrigger == configForCurrentItem.AxisType)
                axisSetStatus = _vGenWrapper.vbox_SetTriggerL(_config.GamepadId, (byte)(value * byte.MaxValue));
            else if (AxisType.RTrigger == configForCurrentItem.AxisType)
                axisSetStatus = _vGenWrapper.vbox_SetTriggerR(_config.GamepadId, (byte)(value * byte.MaxValue));
            return axisSetStatus;
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
            return -axisToInvert;
        }

        private static bool ConvertToButtonState(byte value)
        {
            return value > 0;
        }
    }
}