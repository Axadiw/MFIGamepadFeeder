using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MFIGamepadFeeder.Gamepads;
using MFIGamepadFeeder.Properties;
using Newtonsoft.Json;
using MFIGamepadShared;
using System.Collections.ObjectModel;

namespace MFIGamepadFeeder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            CurrentGamepadManager = new GamepadManager();
            CurrentGamepadManager.ErrorOccuredEvent += CurrentGamepadManager_ErrorOccuredEvent;
        }

        private GamepadManager CurrentGamepadManager { get; }

        private void LayoutSettings(string SelectedJoyId, ObservableCollection<HidDeviceRepresentation> FoundDevices, HidDeviceRepresentation SelectedDevice, ComboBox HidDeviceCombobox, TextBox DeviceIdTextBox, ComboBox ConfigFileCombobox)
        {
            HidDeviceCombobox.ItemsSource = FoundDevices;
            HidDeviceCombobox.SelectedItem = SelectedDevice;

            DeviceIdTextBox.Text = SelectedJoyId;
            var configFiles = Directory.GetFiles("Configs", "*.*", SearchOption.AllDirectories)
         .Where(s => s.EndsWith(".mficonfiguration"));
            ConfigFileCombobox.ItemsSource = configFiles;
            ConfigFileCombobox.SelectedItem = Settings.Default.SelectedConfigFile.ToString();

            AddLTRTCheckBox.IsChecked = Settings.Default.CheckedLTRT;
            AddBackCheckBox.IsChecked = Settings.Default.CheckedBack;
        }

        private void LoadSettings()
        {
            LayoutSettings(Settings.Default.SelectedJoyId.ToString(), CurrentGamepadManager.FoundDevices,
                CurrentGamepadManager.SelectedDevice,
                HidDeviceCombobox, DeviceIdTextBox, ConfigFileCombobox);
            LayoutSettings(Settings.Default.SelectedJoyId2.ToString(), CurrentGamepadManager.FoundDevices,
                CurrentGamepadManager.SelectedDevice2,
                HidDeviceCombobox2, DeviceIdTextBox2, ConfigFileCombobox2);
            CurrentGamepadManager.Refresh();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            LoadSettings();
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CurrentGamepadManager.Refresh();
        }


        /** 1st Gamepad*/
        private void DeviceIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                uint id = Convert.ToUInt32(DeviceIdTextBox.Text);
                if (id == Convert.ToUInt32(DeviceIdTextBox2.Text))
                    id = 0;
                Settings.Default.SelectedJoyId = id;
                Settings.Default.Save();
            }
            catch (Exception exception)
            {
                Log($"Wrong vJoy Id: {exception.Message}");
            }
        }
        private void HidDeviceCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HidDeviceCombobox.SelectedItem != null && HidDeviceCombobox.SelectedItem.Equals(HidDeviceCombobox2.SelectedItem))
            {
                HidDeviceCombobox.SelectedItem = null;
            }
            Settings.Default.SelectedHidDevice = JsonConvert.SerializeObject(HidDeviceCombobox.SelectedItem);
            Settings.Default.Save();
        }

        private void ConfigFileCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.SelectedConfigFile = ConfigFileCombobox.SelectedItem as string;
            Settings.Default.Save();
        }
        /** 2nd Gamepad*/
        private void DeviceIdTextBox2_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                uint id = Convert.ToUInt32(DeviceIdTextBox2.Text);
                if (id == Convert.ToUInt32(DeviceIdTextBox.Text))
                    id = 0;
                Settings.Default.SelectedJoyId2 = id;
                Settings.Default.Save();
            }
            catch (Exception exception)
            {
                Log($"Wrong vJoy Id: {exception.Message}");
            }
        }
        private void HidDeviceCombobox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HidDeviceCombobox2.SelectedItem != null && HidDeviceCombobox2.SelectedItem.Equals(HidDeviceCombobox.SelectedItem))
            {
                HidDeviceCombobox2.SelectedItem = null;
            }
            Settings.Default.SelectedHidDevice2 = JsonConvert.SerializeObject(HidDeviceCombobox2.SelectedItem);
            Settings.Default.Save();
        }

        private void ConfigFileCombobox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.SelectedConfigFile2 = ConfigFileCombobox2.SelectedItem as string;
            Settings.Default.Save();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            CurrentGamepadManager.DisposeAllThreads();
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
            }
            else if (WindowState.Normal == WindowState)
            {
                NotifyIcon.Visibility = Visibility.Collapsed;
            }
        }

        private void CheckBox_Checked_Change(object sender, RoutedEventArgs e)
        {
            Settings.Default.CheckedLTRT = AddLTRTCheckBox.IsChecked ?? false;
            Settings.Default.CheckedBack = AddBackCheckBox.IsChecked ?? false;
            Settings.Default.Save();
        }

    }
}