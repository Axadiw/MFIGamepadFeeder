using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using HidSharp;

namespace MFIGamepadShared
{
    public class HidManager: IDisposable
    {
        private readonly Thread _hidScanThread;
        public readonly ObservableCollection<HidDeviceRepresentation> FoundDevices;
        public readonly HidDeviceLoader HidDeviceLoader;

        public HidManager()
        {
            HidDeviceLoader = new HidDeviceLoader();
            FoundDevices = new ObservableCollection<HidDeviceRepresentation>();

            _hidScanThread = new Thread(() =>
            {
                while (true)
                {
                    var currentDevices = HidDeviceLoader.GetDevices().Select(hidDevice => new HidDeviceRepresentation(hidDevice)).ToList();

                    if (!Thread.CurrentThread.IsAlive) { break; }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var newDevice in currentDevices.Where(device => !FoundDevices.Contains(device)))
                        {
                            FoundDevices.Add(newDevice);
                        }
                    });


                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
                // ReSharper disable once FunctionNeverReturns
            });
            _hidScanThread.Start();
        }        

        public void Dispose()
        {
            _hidScanThread.Abort();
        }
    }
}