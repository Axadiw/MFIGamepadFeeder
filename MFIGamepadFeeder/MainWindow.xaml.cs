using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using MFIGamepadFeeder.Properties;
using MFIGamepadShared;
using Newtonsoft.Json;
using Reactive.Bindings.Extensions;

namespace MFIGamepadFeeder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            ViewModel.ErrorOccuredEvent += CurrentGamepadManager_ErrorOccuredEvent;

            SetupUi();
        }

        public MainWindowViewModel ViewModel { get; }

        private void SetupUi()
        {
            var gamepadControls = new Dictionary<CheckBox, UIElement[]>
            {
                {ControllerActiveCheckbox1, new UIElement[] {HidDeviceCombobox1, DeviceIdComboBox1, MappingFileCombobox1}},
                {ControllerActiveCheckbox2, new UIElement[] {HidDeviceCombobox2, DeviceIdComboBox2, MappingFileCombobox2}},
                {ControllerActiveCheckbox3, new UIElement[] {HidDeviceCombobox3, DeviceIdComboBox3, MappingFileCombobox3}},
                {ControllerActiveCheckbox4, new UIElement[] {HidDeviceCombobox4, DeviceIdComboBox4, MappingFileCombobox4}}
            };

            foreach (var gamepadControlsPair in gamepadControls)
            {
                var checkBox = gamepadControlsPair.Key;
                var controls = gamepadControlsPair.Value;
                var gamepadActiveObservable = checkBox.ObserveEveryValueChanged(box => (box.IsChecked != null) && box.IsChecked.Value);

                Observable.CombineLatest(ViewModel.IsRunning.AsObservable().Select(b => !b), gamepadActiveObservable)
                    .Subscribe(
                        enableConditions =>
                        {
                            controls.ForEach(visual => visual.IsEnabled = enableConditions.All(b => b));
                            checkBox.IsEnabled = enableConditions.First();
                        });
            }

            ViewModel.IsRunning.AsObservable().Subscribe(b => StartButton.Content = b ? "Stop" : "Start");
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
//            WindowState = WindowState.Minimized;

            HidDeviceCombobox1.ItemsSource = ViewModel.HidManager.FoundDevices.Value;
            HidDeviceCombobox2.ItemsSource = ViewModel.HidManager.FoundDevices.Value;
            HidDeviceCombobox3.ItemsSource = ViewModel.HidManager.FoundDevices.Value;
            HidDeviceCombobox4.ItemsSource = ViewModel.HidManager.FoundDevices.Value;


            DeviceIdComboBox1.ItemsSource = new List<uint> {1, 2, 3, 4};
            DeviceIdComboBox2.ItemsSource = new List<uint> {1, 2, 3, 4};
            DeviceIdComboBox3.ItemsSource = new List<uint> {1, 2, 3, 4};
            DeviceIdComboBox4.ItemsSource = new List<uint> {1, 2, 3, 4};

            var configFiles =
                Directory.GetFiles("Configs", "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".mficonfiguration"));
            var itemsSource = configFiles as string[] ?? configFiles.ToArray();
            MappingFileCombobox1.ItemsSource = itemsSource;
            MappingFileCombobox2.ItemsSource = itemsSource;
            MappingFileCombobox3.ItemsSource = itemsSource;
            MappingFileCombobox4.ItemsSource = itemsSource;

            //            HidDeviceCombobox1.SelectedItem = CurrentGamepadManager.SelectedDevice;
            MappingFileCombobox1.SelectedItem = Settings.Default.SelectedConfigFile;
        }

        private void CurrentGamepadManager_ErrorOccuredEvent(object sender, string errorMessage)
        {
            Log(errorMessage);
        }

        private void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogLabel.Text += $"{DateTime.Now}: {message}{Environment.NewLine}";
                LogLabel.ScrollToEnd();
            });
        }

        private void ShowErrorDialog(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsRunning.Value)
            {
                var hidDevices = new List<HidDeviceRepresentation>
                {
                    HidDeviceCombobox1.SelectedItem as HidDeviceRepresentation,
                    HidDeviceCombobox2.SelectedItem as HidDeviceRepresentation,
                    HidDeviceCombobox3.SelectedItem as HidDeviceRepresentation,
                    HidDeviceCombobox4.SelectedItem as HidDeviceRepresentation
                };

                var devicesIds = new List<uint>
                {
                    (uint) DeviceIdComboBox1.SelectedItem,
                    (uint) DeviceIdComboBox2.SelectedItem,
                    (uint) DeviceIdComboBox3.SelectedItem,
                    (uint) DeviceIdComboBox4.SelectedItem
                };

                var hidMappingPaths = new List<string>
                {
                    MappingFileCombobox1.SelectedItem as string,
                    MappingFileCombobox2.SelectedItem as string,
                    MappingFileCombobox3.SelectedItem as string,
                    MappingFileCombobox4.SelectedItem as string
                };

                ViewModel.Start(hidDevices, devicesIds, hidMappingPaths);
            }
            else
            {
                ViewModel.Stop();
            }
        }

        private void DeviceIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Settings.Default.SelectedJoyId = Convert.ToUInt32(DeviceIdComboBox1.Text);
                Settings.Default.Save();
            }
            catch (Exception exception)
            {
                Log($"Wrong vJoy Id: {exception.Message}");
            }
        }

        private void HidDeviceCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.SelectedHidDevice = JsonConvert.SerializeObject(HidDeviceCombobox1.SelectedItem);
            Settings.Default.Save();
        }

        private void MappingFileCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.SelectedConfigFile = MappingFileCombobox1.SelectedItem as string;
            Settings.Default.Save();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ViewModel.Stop();
        }

        private void NotifyIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            Show();
            Activate();
            Focus();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState.Minimized == WindowState)
            {
                NotifyIcon.Visibility = Visibility.Visible;
                Hide();
            }
            else if (WindowState.Normal == WindowState)
            {
                NotifyIcon.Visibility = Visibility.Collapsed;
            }
        }

        private void DeviceIdTextBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void DeviceIdTextBox4_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void DeviceIdTextBox3_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void DeviceIdTextBox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}