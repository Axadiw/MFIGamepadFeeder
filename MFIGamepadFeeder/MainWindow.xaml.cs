using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MFIGamepadFeeder.Gamepads;
using MFIGamepadFeeder.Properties;
using Newtonsoft.Json;

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

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
//            WindowState = WindowState.Minimized;

            HidDeviceCombobox.ItemsSource = CurrentGamepadManager.FoundDevices;
            HidDeviceCombobox.SelectedItem = CurrentGamepadManager.SelectedDevice;

            DeviceIdTextBox.Text = Settings.Default.SelectedJoyId.ToString();

            var configFiles =
                Directory.GetFiles("Configs", "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".mficonfiguration"));
            ConfigFileCombobox.ItemsSource = configFiles;
            ConfigFileCombobox.SelectedItem = Settings.Default.SelectedConfigFile;

            CurrentGamepadManager.Refresh();
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

        private void DeviceIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Settings.Default.SelectedJoyId = Convert.ToUInt32(DeviceIdTextBox.Text);
                Settings.Default.Save();
            }
            catch (Exception exception)
            {
                Log($"Wrong vJoy Id: {exception.Message}");
            }
        }

        private void HidDeviceCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.SelectedHidDevice = JsonConvert.SerializeObject(HidDeviceCombobox.SelectedItem);
            Settings.Default.Save();
        }

        private void ConfigFileCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.SelectedConfigFile = ConfigFileCombobox.SelectedItem as string;
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
                Hide();
            }
            else if (WindowState.Normal == WindowState)
            {
                NotifyIcon.Visibility = Visibility.Collapsed;
            }
        }
    }
}