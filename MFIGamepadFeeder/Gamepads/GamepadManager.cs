using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HidSharp;
using MFIGamepadFeeder.Properties;
using MFIGamepadShared;
using MFIGamepadShared.Configuration;
using Newtonsoft.Json;
using vGenWrapper;

namespace MFIGamepadFeeder.Gamepads
{
    internal class GamepadManager
    {
        private readonly Thread _currentDeviceUpdateThread;
        private readonly HidDeviceLoader _hidDeviceLoader;
        private readonly VGenWrapper _vGenWrapper;

        private Thread _currentGamepadThread;

        public GamepadManager()
        {
            _hidDeviceLoader = new HidDeviceLoader();
            _vGenWrapper = new VGenWrapper();
            FoundDevices = new ObservableCollection<HidDeviceRepresentation>();

            SelectedDevice = JsonConvert.DeserializeObject<HidDeviceRepresentation>(Settings.Default.SelectedHidDevice);
            if ((SelectedDevice != null) && !FoundDevices.Contains(SelectedDevice))
                FoundDevices.Add(SelectedDevice);

            _currentDeviceUpdateThread = new Thread(() =>
            {
                while (true)
                {
                    var hidDevices = _hidDeviceLoader.GetDevices();
                    var currentDevices =
                        hidDevices.Select(hidDevice => new HidDeviceRepresentation(hidDevice)).ToList();

                    foreach (var newDevice in currentDevices.Where(device => !FoundDevices.Contains(device)))
                        Application.Current.Dispatcher.Invoke(() => FoundDevices.Add(newDevice));

                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
                // ReSharper disable once FunctionNeverReturns
            });
            _currentDeviceUpdateThread.Start();
        }

        public ObservableCollection<HidDeviceRepresentation> FoundDevices { get; }
        public HidDeviceRepresentation SelectedDevice { get; set; }

        public event ErrorOccuredEventHandler ErrorOccuredEvent;

        public void Refresh()
        {
            _currentGamepadThread?.Abort();
            _currentGamepadThread = new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                var selectedConfigFile = Settings.Default.SelectedConfigFile;
                var selectedHidDevice = Settings.Default.SelectedHidDevice;
                var vJoyId = Settings.Default.SelectedJoyId;

                if ((selectedConfigFile == string.Empty) || (selectedHidDevice == string.Empty))
                {
                    Log("Configuration incomplete");
                    return;
                }                

                GamepadConfiguration configuration = null;
                HidDeviceRepresentation hidDeviceRepresentation = null;

                try
                {
                    configuration = await GetConfigFromFilePath(selectedConfigFile);
                    hidDeviceRepresentation =
                        JsonConvert.DeserializeObject<HidDeviceRepresentation>(selectedHidDevice);
                }
                catch (Exception ex)
                {
                    Log($"Error while reading configuration: {ex.Message}");
                }

                Log($"Using {hidDeviceRepresentation}, vJoy {vJoyId}, configuration file: {selectedConfigFile}");

                var vBusExists = _vGenWrapper.vbox_isVBusExist();
                if (vBusExists == NtStatus.Success)
                {
                    Log("XBox bus installed");
                }
                else
                {
                    Log($"XBox bus not installed ({vBusExists})");
                }
                

                SetupGamepad(hidDeviceRepresentation, vJoyId, configuration);
            });
            _currentGamepadThread.Start();
        }

        public void SetupGamepad(HidDeviceRepresentation hidDeviceRepresentation, uint gamePadId,
            GamepadConfiguration config)
        {
            try
            {
                var device =
                    _hidDeviceLoader.GetDevices(
                        hidDeviceRepresentation.VendorId,
                        hidDeviceRepresentation.ProductId,
                        hidDeviceRepresentation.ProductVersion,
                        hidDeviceRepresentation.SerialNumber
                    ).First();


                if (device == null)
                {
                    Log(@"Failed to open device.");
                    return;
                }

                HidStream stream;
                if (!device.TryOpen(out stream))
                {
                    Log("Failed to open device.");
                    return;
                }

                var gamepad = new Gamepad(config, gamePadId, _vGenWrapper, Gamepad_ErrorOccuredEvent);

                Log("Successfully initialized gamepad");

                using (stream)
                {
                    while (true)
                    {
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
                            gamepad.UpdateState(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        private async Task<GamepadConfiguration> GetConfigFromFilePath(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                var fileText = await reader.ReadToEndAsync();
                return new GamepadConfiguration(fileText);
            }
        }

        private void Gamepad_ErrorOccuredEvent(object sender, string errorMessage)
        {
            Log(errorMessage);
        }

        protected virtual void Log(string errormessage)
        {
            ErrorOccuredEvent?.Invoke(this, errormessage);
        }

        public void DisposeAllThreads()
        {
            _currentDeviceUpdateThread.Abort();
            _currentGamepadThread.Abort();
        }
    }
}