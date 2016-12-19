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

        public Dictionary<CheckBox, ComboBox[]> GamepadControls { get; set; }

        public MainWindowViewModel ViewModel { get; }

        private void SetupUi()
        {
            GamepadControls = new Dictionary<CheckBox, ComboBox[]>
            {
                {ControllerActiveCheckbox1, new[] {HidDeviceCombobox1, DeviceIdComboBox1, MappingFileCombobox1}},
                {ControllerActiveCheckbox2, new[] {HidDeviceCombobox2, DeviceIdComboBox2, MappingFileCombobox2}},
                {ControllerActiveCheckbox3, new[] {HidDeviceCombobox3, DeviceIdComboBox3, MappingFileCombobox3}},
                {ControllerActiveCheckbox4, new[] {HidDeviceCombobox4, DeviceIdComboBox4, MappingFileCombobox4}}
            };

            foreach (var gamepadControlsPair in GamepadControls)
            {
                var checkBox = gamepadControlsPair.Key;
                var controls = gamepadControlsPair.Value;
                var gamepadActiveObservable = checkBox.ObserveEveryValueChanged(box => (box.IsChecked != null) && box.IsChecked.Value);

                Observable.CombineLatest(ViewModel.IsRunning.AsObservable().Select(b => !b), gamepadActiveObservable)
                    .ObserveOn(Application.Current.Dispatcher)
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
            if (Settings.Default.StartMinimized)
            {
                WindowState = WindowState.Minimized;
            }

            HidDeviceCombobox1.ItemsSource = ViewModel.HidManager.FoundDevices;
            HidDeviceCombobox2.ItemsSource = ViewModel.HidManager.FoundDevices;
            HidDeviceCombobox3.ItemsSource = ViewModel.HidManager.FoundDevices;
            HidDeviceCombobox4.ItemsSource = ViewModel.HidManager.FoundDevices;

            try
            {
                HidDeviceCombobox1.SelectedItem = JsonConvert.DeserializeObject<HidDeviceRepresentation>(Settings.Default.SelectedHidDevice1);
                HidDeviceCombobox2.SelectedItem = JsonConvert.DeserializeObject<HidDeviceRepresentation>(Settings.Default.SelectedHidDevice2);
                HidDeviceCombobox3.SelectedItem = JsonConvert.DeserializeObject<HidDeviceRepresentation>(Settings.Default.SelectedHidDevice3);
                HidDeviceCombobox4.SelectedItem = JsonConvert.DeserializeObject<HidDeviceRepresentation>(Settings.Default.SelectedHidDevice4);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }

            var deviceIds = new List<uint> {1, 2, 3, 4};
            DeviceIdComboBox1.ItemsSource = deviceIds;
            DeviceIdComboBox2.ItemsSource = deviceIds;
            DeviceIdComboBox3.ItemsSource = deviceIds;
            DeviceIdComboBox4.ItemsSource = deviceIds;
            DeviceIdComboBox1.SelectedItem = Settings.Default.SelectedControllerId1;
            DeviceIdComboBox2.SelectedItem = Settings.Default.SelectedControllerId2;
            DeviceIdComboBox3.SelectedItem = Settings.Default.SelectedControllerId3;
            DeviceIdComboBox4.SelectedItem = Settings.Default.SelectedControllerId4;

            var configFiles =
                Directory.GetFiles("Configs", "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".mficonfiguration"));
            var itemsSource = configFiles as string[] ?? configFiles.ToArray();
            MappingFileCombobox1.ItemsSource = itemsSource;
            MappingFileCombobox2.ItemsSource = itemsSource;
            MappingFileCombobox3.ItemsSource = itemsSource;
            MappingFileCombobox4.ItemsSource = itemsSource;

            MappingFileCombobox1.SelectedItem = Settings.Default.SelectedConfigFile1;
            MappingFileCombobox2.SelectedItem = Settings.Default.SelectedConfigFile2;
            MappingFileCombobox3.SelectedItem = Settings.Default.SelectedConfigFile3;
            MappingFileCombobox4.SelectedItem = Settings.Default.SelectedConfigFile4;

            ControllerActiveCheckbox1.IsChecked = Settings.Default.ControllerActive1;
            ControllerActiveCheckbox2.IsChecked = Settings.Default.ControllerActive2;
            ControllerActiveCheckbox3.IsChecked = Settings.Default.ControllerActive3;
            ControllerActiveCheckbox4.IsChecked = Settings.Default.ControllerActive4;

            if (Settings.Default.AutoPlugIn)
            {
                StartButton_Click(null, null);
            }
        }

        private void DeviceId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if ((DeviceIdComboBox1.SelectedItem != null) && ReferenceEquals(sender, DeviceIdComboBox1))
                {
                    Settings.Default.SelectedControllerId1 = (uint) DeviceIdComboBox1.SelectedItem;
                }
                if ((DeviceIdComboBox2.SelectedItem != null) && ReferenceEquals(sender, DeviceIdComboBox2))
                {
                    Settings.Default.SelectedControllerId2 = (uint) DeviceIdComboBox2.SelectedItem;
                }
                if ((DeviceIdComboBox3.SelectedItem != null) && ReferenceEquals(sender, DeviceIdComboBox3))
                {
                    Settings.Default.SelectedControllerId3 = (uint) DeviceIdComboBox3.SelectedItem;
                }
                if ((DeviceIdComboBox4.SelectedItem != null) && ReferenceEquals(sender, DeviceIdComboBox4))
                {
                    Settings.Default.SelectedControllerId4 = (uint) DeviceIdComboBox4.SelectedItem;
                }

                Settings.Default.Save();
            }
            catch (Exception exception)
            {
                Log($"Wrong controller Id: {exception.Message}");
            }
        }

        private void ControllerActiveCheckbox_IsCheckedChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            if ((ControllerActiveCheckbox1.IsChecked != null) && ReferenceEquals(sender, ControllerActiveCheckbox1))
            {
                Settings.Default.ControllerActive1 = ControllerActiveCheckbox1.IsChecked.Value;
            }
            if ((ControllerActiveCheckbox2.IsChecked != null) && ReferenceEquals(sender, ControllerActiveCheckbox2))
            {
                Settings.Default.ControllerActive2 = ControllerActiveCheckbox2.IsChecked.Value;
            }
            if ((ControllerActiveCheckbox3.IsChecked != null) && ReferenceEquals(sender, ControllerActiveCheckbox3))
            {
                Settings.Default.ControllerActive3 = ControllerActiveCheckbox3.IsChecked.Value;
            }
            if ((ControllerActiveCheckbox4.IsChecked != null) && ReferenceEquals(sender, ControllerActiveCheckbox4))
            {
                Settings.Default.ControllerActive4 = ControllerActiveCheckbox4.IsChecked.Value;
            }
            Settings.Default.Save();
        }

        private void HidDeviceCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, HidDeviceCombobox1))
            {
                Settings.Default.SelectedHidDevice1 = JsonConvert.SerializeObject(HidDeviceCombobox1.SelectedItem);
            }
            if (ReferenceEquals(sender, HidDeviceCombobox2))
            {
                Settings.Default.SelectedHidDevice2 = JsonConvert.SerializeObject(HidDeviceCombobox2.SelectedItem);
            }
            if (ReferenceEquals(sender, HidDeviceCombobox3))
            {
                Settings.Default.SelectedHidDevice3 = JsonConvert.SerializeObject(HidDeviceCombobox3.SelectedItem);
            }
            if (ReferenceEquals(sender, HidDeviceCombobox4))
            {
                Settings.Default.SelectedHidDevice4 = JsonConvert.SerializeObject(HidDeviceCombobox4.SelectedItem);
            }
            Settings.Default.Save();
        }

        private void MappingFileCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, MappingFileCombobox1))
            {
                Settings.Default.SelectedConfigFile1 = MappingFileCombobox1.SelectedItem as string;
            }
            if (ReferenceEquals(sender, MappingFileCombobox2))
            {
                Settings.Default.SelectedConfigFile2 = MappingFileCombobox2.SelectedItem as string;
            }
            if (ReferenceEquals(sender, MappingFileCombobox3))
            {
                Settings.Default.SelectedConfigFile3 = MappingFileCombobox3.SelectedItem as string;
            }
            if (ReferenceEquals(sender, MappingFileCombobox4))
            {
                Settings.Default.SelectedConfigFile4 = MappingFileCombobox4.SelectedItem as string;
            }

            Settings.Default.Save();
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

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsRunning.Value)
            {
                var activeGamepads = GamepadControls.Keys.Where(box => (box.IsChecked != null) && box.IsChecked.Value).ToList();
                var allComboboxes = new List<ComboBox>();
                activeGamepads.ForEach(box => allComboboxes.AddRange(GamepadControls[box]));
                var doesAllSelectedItemsHaveValue = allComboboxes.All(box => box.SelectedItem != null);

                if (!doesAllSelectedItemsHaveValue)
                {
                    Log("Configuration incomplete");
                    return;
                }

                var hidDevices = activeGamepads.Select(activeGamepad => GamepadControls[activeGamepad][0]).Select(box => box.SelectedItem as HidDeviceRepresentation).ToArray();
                var devicesIds = activeGamepads.Select(activeGamepad => GamepadControls[activeGamepad][1]).Select(box => (uint) box.SelectedItem).ToArray();
                var hidMappingPaths = activeGamepads.Select(activeGamepad => GamepadControls[activeGamepad][2]).Select(box => box.SelectedItem as string).ToArray();

                var areThereAnyDuplicatesInDevicesIds = devicesIds.GroupBy(x => x).Count(x => x.Count() > 1) > 0;


                if (areThereAnyDuplicatesInDevicesIds)
                {
                    Log("You can't use the same controller ID twice!");
                    return;
                }

                ViewModel.Start(hidDevices, devicesIds, hidMappingPaths);
            }
            else
            {
                ViewModel.Stop();
            }
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ViewModel.Dispose();
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

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var optionsWindow = new OptionsWindow();
            optionsWindow.ShowDialog();
        }

        private void EditorButton_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}