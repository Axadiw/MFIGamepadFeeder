using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using MFIGamepadFeeder;
using MFIGamepadShared.Configuration;
using vGenWrapper;

namespace MFIGamepadShared
{
    public class GamepadManager
    {
        private readonly HidManager _hidManager;
        private readonly VGenWrapper _vGenWrapper;

        public GamepadManager(VGenWrapper vGenWrapper, HidManager hidManager)
        {
            _vGenWrapper = vGenWrapper;
            _hidManager = hidManager;
        }

        public Exception GamepadStartException => new Exception();
        public Exception NoXboxBusPresetException => new Exception();
        public event ErrorOccuredEventHandler ErrorOccuredEvent;

        public IObservable<uint> Start(List<GamepadConfiguration> gamepadConfigurations)
        {
            return Observable.Start(() => _vGenWrapper.vbox_isVBusExist())
                .Select(status =>
                {
                    if (status == NtStatus.Success)
                    {
                        Log("XBox bus installed");
                        return null;
                    }

                    Log($"XBox bus not installed ({status})");
                    return Observable.Throw<uint>(NoXboxBusPresetException);
                })
                .SelectMany(_ => gamepadConfigurations.Select(StartSingle).Merge());
        }


        private IObservable<uint> StartSingle(GamepadConfiguration gamepadConfiguration)
        {
            return Observable.Create<uint>(observer =>
            {
                var gamepad = new Gamepad(gamepadConfiguration, _vGenWrapper, _hidManager.HidDeviceLoader);
                gamepad.ErrorOccuredEvent += Gamepad_ErrorOccuredEvent;
                if (gamepad.Start())
                {
                    observer.OnNext(gamepadConfiguration.GamepadId);
                }
                else
                {
                    gamepad.Dispose();
                    observer.OnError(GamepadStartException);
                }

                return Disposable.Create(() =>
                {
                    gamepad.Dispose();
                    gamepad.ErrorOccuredEvent -= Gamepad_ErrorOccuredEvent;
                });
            });
        }

        private void Gamepad_ErrorOccuredEvent(object sender, string errorMessage)
        {
            Log(errorMessage);
        }

        protected virtual void Log(string errormessage)
        {
            ErrorOccuredEvent?.Invoke(this, errormessage);
        }
    }
}