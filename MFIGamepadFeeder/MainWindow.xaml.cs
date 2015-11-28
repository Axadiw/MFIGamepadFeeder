using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HidSharp;
using MFIGamepadShared.Configuration;

namespace MFIGamepadFeeder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void SetupGamepad(int? vendorId, int? productId, int? productVersion, string serialNumber,
            uint gamePadId)
        {
            var loader = new HidDeviceLoader();

            var device = loader.GetDevices(vendorId, productId, productVersion, serialNumber).First();
            if (device == null)
            {
                ShowErrorDialog(@"Failed to open device.");
                return;
            }

            HidStream stream;
            if (!device.TryOpen(out stream))
            {
                ShowErrorDialog("Failed to open device.");
                return;
            }

            var gamepad = new Gamepad(await GetConfigFromFilePath("Configs/Nimbus.mficonfiguration"), gamePadId);
            gamepad.ErrorOccuredEvent += Gamepad_ErrorOccuredEvent;

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

                    if (count > 0)
                    {
                        gamepad.UpdateState(bytes);
                    }
                }
            }
        }

        private void Gamepad_ErrorOccuredEvent(object sender, string errorMessage)
        {
            ShowErrorDialog(errorMessage);
        }

        private void ShowErrorDialog(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                SetupGamepad(null, null, null, "289a4b11e297", 1);
            }).Start();
        }

        private async Task<GamepadConfiguration> GetConfigFromFilePath(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                var fileText = await reader.ReadToEndAsync();
                return new GamepadConfiguration(fileText);
            }
        }
    }
}