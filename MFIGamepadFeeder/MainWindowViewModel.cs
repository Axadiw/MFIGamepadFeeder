using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MFIGamepadShared;
using MFIGamepadShared.Configuration;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using vGenWrapper;

namespace MFIGamepadFeeder
{
    public class MainWindowViewModel: IDisposable
    {
        private readonly GamepadManager _gamepadManager;
        public readonly ReactiveProperty<bool> IsRunning = new ReactiveProperty<bool>(false);
        private IDisposable _runningGamepadsDisposable;
        public HidManager HidManager;        

        public MainWindowViewModel()
        {
            var vGenWrapper = new VGenWrapper();
            HidManager = new HidManager();
            _gamepadManager = new GamepadManager(vGenWrapper, HidManager);
            _gamepadManager.ErrorOccuredEvent += (_, message) => { Log(message); };

            IsRunning.AsObservable().Where(b => !b).Subscribe(_ => _runningGamepadsDisposable?.Dispose());
        }
        

        public event ErrorOccuredEventHandler ErrorOccuredEvent;

        public async void Start(HidDeviceRepresentation[] selectedHids, uint[] selectedGamepadIds, string[] selectedMappsingPaths)
        {
            try
            {
                var mappings = new List<string>();
                foreach (var selectedMappsingPath in selectedMappsingPaths)
                {
                    var mapping = await GetMappingFromFilePath(selectedMappsingPath);
                    mappings.Add(mapping);
                }
                
                var configs = selectedHids.Select((t, i) => new GamepadConfiguration(selectedGamepadIds[i], mappings[i], t)).ToList();

                _runningGamepadsDisposable = _gamepadManager.Start(configs).Subscribe(
                    gamepadId => { },
                    exception => { IsRunning.Value = false; });

                IsRunning.Value = true;
            }
            catch (Exception ex)
            {
                IsRunning.Value = false;
                Log(ex.Message);
            }
        }

        public void Stop()
        {
            IsRunning.Value = false;
        }

        private static async Task<string> GetMappingFromFilePath(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                return await reader.ReadToEndAsync();
            }
        }

        protected virtual void Log(string errormessage)
        {
            ErrorOccuredEvent?.Invoke(this, errormessage);
        }

        public void Dispose()
        {
            Stop();
            HidManager.Dispose();
        }
    }
}