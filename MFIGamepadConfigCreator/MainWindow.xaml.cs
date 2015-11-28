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
            Log("Will save Nimbus config file");
            await SaveConfigurationToFile(configCreator.GetNimbusConfiguration(), "Nimbus");
            Log("Save successful");
        }

        private void Log(string message)
        {
            logLabel.Content += message + "\n";
        }

        private static async Task SaveConfigurationToFile(GamepadConfiguration configuration, string filename)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            using (var outputFile = new StreamWriter(desktopPath + $"\\{filename}.mficonfiguration"))
            {
                await outputFile.WriteAsync(configuration.GetJsonRepresentation());
            }
        }
    }
}