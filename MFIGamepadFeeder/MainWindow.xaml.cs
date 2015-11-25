using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using HidSharp;

namespace MFIGamepadFeeder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
       

        private static void SetupGamepad()
        {
            var loader = new HidDeviceLoader();
            Thread.Sleep(2000); // Give a bit of time so our timing below is more valid as a benchmark.

            var stopwatch = new Stopwatch();
            var deviceList = loader.GetDevices().ToArray();

            var device = loader.GetDevices(null, null, null, "289a4b11e297").First();
            if (device == null)
            {
                Console.WriteLine("Failed to open device.");
                return;
            }

            HidStream stream;
            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("Failed to open device.");
                return;
            }



            Gamepad gamepad = new Gamepad(device.MaxInputReportLength);

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
                        gamepad.Gamepad_NewState(bytes);
                    }
                }
            }
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            SetupGamepad();
        }
    }
}