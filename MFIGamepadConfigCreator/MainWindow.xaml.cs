using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MFIGamepadShared.Configuration;

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
            Log("Nimbus config file save...");
            await SaveConfigurationToFile(configCreator.GetNimbusConfiguration(), "Nimbus");
            Log("Success!");
        }

        private void Log(string message)
        {
            logLabel.Content += message + "\n";
        }

        private async Task SaveConfigurationToFile(GamepadConfiguration configuration, string filename)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var path = desktopPath + $"\\{filename}.mficonfiguration";
            using (var outputFile = new StreamWriter(path))
            {
                Log("Write " + path + "...");
                await outputFile.WriteAsync(configuration.GetJsonRepresentation());                
            }
        }
    }
}