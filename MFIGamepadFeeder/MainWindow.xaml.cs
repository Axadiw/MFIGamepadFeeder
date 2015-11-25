using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using HidSharp;
using MFIGamepadFeeder.Gamepads.Configuration;

namespace MFIGamepadFeeder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SetupGamepad(int? vendorId, int? productId, int? productVersion, string serialNumber,
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


            var gamepad = new Gamepad(GetConfiguration(), gamePadId);
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

        private GamepadConfiguration GetConfiguration()
        {
            var configItems = new Collection<GamepadConfigurationItem>
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

            return new GamepadConfiguration {ConfigItems = configItems};
        }
    }
}