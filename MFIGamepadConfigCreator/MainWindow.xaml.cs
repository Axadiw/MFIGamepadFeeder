using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MFIGamepadShared.Configuration;
using Newtonsoft.Json;

namespace MFIGamepadConfigCreator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var configCreator = new ConfigCreator();
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Log("Will save Nimbus config file");
            await SaveConfigurationToFile(configCreator.GetNimbusConfiguration(), "Nimbus", desktopPath);
            Log("Save successful to " + desktopPath);
            Log("Will save Nimbus config file");
            await SaveConfigurationToFile(configCreator.GetMiniConfiguration(), "Mini", desktopPath);
            Log("Save successful to " + desktopPath);
        }

        private void Log(string message)
        {
            logLabel.Content += message + "\n";
        }

        private static async Task SaveConfigurationToFile(GamepadMapping configuration, string filename, string desktopPath)
        {
            using (var outputFile = new StreamWriter(desktopPath + $"\\{filename}.mfimapping"))
            {
                await outputFile.WriteAsync(JsonConvert.SerializeObject(configuration, Formatting.Indented));
            }
        }
    }
}