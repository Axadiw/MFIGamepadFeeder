using System;
using System.Linq;
using System.Threading;
using HidSharp;
using MFIGamepadShared;
using Reactive.Bindings;

namespace MFIGamepadFeeder
{
    internal class SimplifiedHidPreview : IDisposable
    {
        private Thread _updateThread;

        public ReactiveProperty<string> CurrentHidState;

        public SimplifiedHidPreview()
        {
            HidManager = new HidManager();
            CurrentHidState = new ReactiveProperty<string>("");
        }

        public HidManager HidManager { get; set; }

        public void Dispose()
        {
            HidManager.Dispose();
            _updateThread?.Abort();
        }

        public bool PlugInToHidDeviceAndStartLoop(HidDeviceRepresentation representation)
        {
            var device =
                HidManager.HidDeviceLoader.GetDevices(
                    representation.VendorId,
                    representation.ProductId,
                    representation.ProductVersion,
                    representation.SerialNumber
                ).First();


            if (device == null)
            {
                return false;
            }

            HidStream stream;
            if (!device.TryOpen(out stream))
            {
                return false;
            }

            _updateThread?.Abort();
            _updateThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    using (stream)
                    {
                        while (true)
                        {
                            if (!Thread.CurrentThread.IsAlive)
                            {
                                break;
                            }

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
                            catch
                            {
                                break;
                            }

                            if (count > 0)
                            {
                                CurrentHidState.Value = string.Join(" ", bytes);
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            });
            _updateThread.Start();

            return true;
        }
    }
}