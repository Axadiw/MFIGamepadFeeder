using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;
using Reactive.Bindings;

namespace MFIGamepadShared
{
    public class HidManager
    {
        private readonly Thread _hidScanThread;
        public readonly ReactiveProperty<List<HidDeviceRepresentation>> FoundDevices;
        public readonly HidDeviceLoader HidDeviceLoader;

        public HidManager()
        {
            HidDeviceLoader = new HidDeviceLoader();
            FoundDevices = new ReactiveProperty<List<HidDeviceRepresentation>>(new List<HidDeviceRepresentation>());

            _hidScanThread = new Thread(() =>
            {
                while (true)
                {
                    var hidDevices = HidDeviceLoader.GetDevices();
                    var currentDevices =
                        hidDevices.Select(hidDevice => new HidDeviceRepresentation(hidDevice)).ToList();

                    foreach (var newDevice in currentDevices.Where(device => !FoundDevices.Value.Contains(device)))
                    {
                        FoundDevices.Value.Add(newDevice);
                    }


                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
                // ReSharper disable once FunctionNeverReturns
            });
            _hidScanThread.Start();
        }


        ~HidManager()
        {
            _hidScanThread.Abort();
        }
    }
}