using System;
using System.Windows;
using NBug;

namespace MFIGamepadFeeder
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Settings.ReleaseMode = true;

            AppDomain.CurrentDomain.UnhandledException += Handler.UnhandledException;
            Current.DispatcherUnhandledException += Handler.DispatcherUnhandledException;
        }
    }
}