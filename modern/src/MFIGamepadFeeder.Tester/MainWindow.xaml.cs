using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Diagnostics;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Tester;

public partial class MainWindow : Window
{
    private readonly HidDeviceService _devices = new();
    private readonly DispatcherTimer _refresh = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private HidTestSession? _session;
    private string? _listeningPath;
    private bool _scanning;
    private bool _closing;
    private bool _closeReady;
    private bool _manuallyStopped;
    private long _historyPresses = -1;
    private long _historyReleases = -1;
    private readonly HashSet<uint> _connectedXboxSlots = [];
    private Task<Dictionary<uint, GamepadState>>? _xboxRead;
    private long _nextXboxProbe;
    private GamepadState _lastRenderedDirect;
    private GamepadState _lastRenderedXbox;
    private long _lastVisualVersion = -1;
    private readonly Queue<double> _uiUpdateDelays = new();
    private bool _renderAttached;
    private TimeSpan _lastRenderingTime = TimeSpan.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _refresh.Tick += (_, _) => RefreshDisplay();
        _devices.DeviceListChanged += DeviceListChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_renderAttached)
        {
            CompositionTarget.Rendering += RenderLatestInput;
            _renderAttached = true;
        }
        _refresh.Start();
        await ScanDevicesAsync();
    }

    private void DeviceListChanged()
    {
        if (_closing || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(async () => await ScanDevicesAsync()));
    }

    private async Task ScanDevicesAsync()
    {
        if (_scanning || _closing) return;
        _scanning = true;
        RefreshButton.IsEnabled = false;
        RefreshButton.Content = "Checking…";
        try
        {
            var previous = DeviceComboBox.SelectedItem as HidDeviceInfo;
            var devices = await Task.Run(() => _devices.GetCurrentDevices()
                .Where(d => d.VendorId == 0x0111 && d.ProductId is 0x1420 or 0x1421 or 0x1422).ToList());
            if (_closing) return;
            // Keep the active target selected while its reader waits for a Bluetooth reconnect.
            if (_session is not null && previous is not null && devices.All(d => d.DevicePath != previous.DevicePath))
                devices.Insert(0, previous);
            DeviceComboBox.ItemsSource = devices;
            DeviceComboBox.SelectedItem = devices.FirstOrDefault(d => d.DevicePath == previous?.DevicePath) ?? devices.FirstOrDefault();
            if (devices.Count == 0)
            {
                StatusText.Text = "No active Nimbus input interface found";
                StatusDetail.Text = "Wake your Nimbus, check Bluetooth says Connected, then choose Refresh devices. Paired alone may mean it is asleep.";
                ListenButton.IsEnabled = false;
            }
            else if (!_manuallyStopped && _listeningPath != (DeviceComboBox.SelectedItem as HidDeviceInfo)?.DevicePath)
            {
                await StartSelectedAsync();
            }
            else
            {
                ListenButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not check controllers";
            StatusDetail.Text = ex.Message + " Choose Refresh devices to retry.";
        }
        finally
        {
            _scanning = false;
            if (!_closing)
            {
                RefreshButton.IsEnabled = true;
                RefreshButton.Content = "Refresh devices";
            }
        }
    }

    private async Task StartSelectedAsync()
    {
        await _sessionGate.WaitAsync();
        try
        {
            if (_closing || DeviceComboBox.SelectedItem is not HidDeviceInfo device) return;
            DeviceComboBox.IsEnabled = false;
            ListenButton.IsEnabled = false;
            await StopCurrentAsync();
            if (_closing) return;

            var configPath = Path.Combine(AppContext.BaseDirectory, "Configs", device.KnownProfile!.RecommendedConfigFileName);
            var mapping = await Task.Run(() => MappingSerializer.LoadFromFile(configPath));
            if (_closing) return;
            mapping.VirtualKeysItems.Clear();
            mapping.Deadzone = 0;
            mapping.ButtonThreshold = 1;
            _session = new HidTestSession(new HidSharpReportSource(device), new HidReportMapper(mapping));
            _listeningPath = device.DevicePath;
            _historyPresses = _historyReleases = -1;
            _lastVisualVersion = -1;
            _uiUpdateDelays.Clear();
            _manuallyStopped = false;
            DeviceDetailsText.Text = $"VID {device.VendorId:X4} · PID {device.ProductId:X4} · Maximum report: {device.MaxInputReportLength} bytes\n"
                + (device.IsHidGamepadUsage ? "Standard HID gamepad interface detected." : "Standard HID gamepad usage was not detected.");
            _session.Start();
            ListenButton.Content = "Stop test";
        }
        catch (Exception ex)
        {
            await StopCurrentAsync();
            StatusText.Text = "Could not start the input test";
            StatusDetail.Text = ex.Message + " Refresh the controller list and retry.";
        }
        finally
        {
            if (!_closing)
            {
                DeviceComboBox.IsEnabled = true;
                ListenButton.IsEnabled = DeviceComboBox.SelectedItem is not null;
            }
            _sessionGate.Release();
        }
    }

    private async Task StopCurrentAsync()
    {
        var session = _session;
        _session = null;
        _listeningPath = null;
        if (session is not null) await session.DisposeAsync();
        DirectView.Update(GamepadState.Neutral);
        _lastRenderedDirect = GamepadState.Neutral;
        RawText.Text = "No report yet.";
        CadenceText.Text = "Report timing appears after input arrives.";
        ReportText.Text = "Waiting for input reports.";
        ListenButton.Content = "Start test";
    }

    private void RenderLatestInput(object? sender, EventArgs e)
    {
        if (_closing || WindowState == WindowState.Minimized) return;
        if (e is RenderingEventArgs frame)
        {
            if (frame.RenderingTime == _lastRenderingTime) return;
            _lastRenderingTime = frame.RenderingTime;
        }
        if (_session is not null)
        {
            var visual = _session.GetVisualState();
            if (visual.Version != _lastVisualVersion)
            {
                _lastVisualVersion = visual.Version;
                _lastRenderedDirect = visual.State;
                DirectView.Update(visual.State, updateLabels: false);
                if (visual.ReceivedTimestamp is { } received)
                {
                    _uiUpdateDelays.Enqueue(Stopwatch.GetElapsedTime(received).TotalMilliseconds);
                    if (_uiUpdateDelays.Count > 128) _uiUpdateDelays.Dequeue();
                }
            }
        }
        if (XboxExpander.IsExpanded) RefreshXboxDisplay();
    }

    private void RefreshDisplay()
    {
        if (_session is not null)
        {
            var snapshot = _session.GetSnapshot();
            StatusText.Text = snapshot.Status switch
            {
                HidTestStatus.Receiving => "Receiving Nimbus input directly",
                HidTestStatus.Waiting => "Listening for Nimbus input",
                HidTestStatus.Connecting => "Opening the Nimbus input interface…",
                HidTestStatus.Disconnected => "Nimbus disconnected · waiting to reconnect",
                HidTestStatus.Faulted => "Input reader needs attention",
                _ => "Input test stopped",
            };
            StatusDetail.Text = snapshot.Message;
            // Numeric text/history run at 10 Hz; stick and button visuals follow the render cadence.
            DirectView.Update(_lastRenderedDirect);
            CountsText.Text = $"{snapshot.ButtonPressCount} presses · {snapshot.ButtonReleaseCount} releases";
            var lastAge = snapshot.LastReportAtUtc is { } last ? Math.Max(0, (DateTimeOffset.UtcNow - last).TotalSeconds) : (double?)null;
            ReportText.Text = snapshot.ReceivedReportCount == 0 ? "Waiting for the first input report."
                : $"{snapshot.ReceivedReportCount:N0} received · {snapshot.ReportCount:N0} decoded · {(lastAge < 1 ? $"{snapshot.ReportsPerSecond:0.#} reports/s" : "No recent reports")} · Last report {lastAge:0.0}s ago";
            if (snapshot.ButtonPressCount != _historyPresses || snapshot.ButtonReleaseCount != _historyReleases)
            {
                _historyPresses = snapshot.ButtonPressCount;
                _historyReleases = snapshot.ButtonReleaseCount;
                HistoryHint.Visibility = snapshot.RecentEvents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                HistoryList.ItemsSource = snapshot.RecentEvents.Reverse().Select(e =>
                    $"{e.Timestamp.ToLocalTime():HH:mm:ss.fff}   {ButtonName(e.Button)} {(e.IsPressed ? "pressed" : "released")}").ToArray();
            }
            if (RawExpander.IsExpanded)
            {
                CadenceText.Text = snapshot.AverageReportIntervalMs is { } average
                    ? $"Recent intervals: average {average:0.00} ms · max {snapshot.MaxReportIntervalMs:0.00} ms"
                    : "Move a stick to collect consecutive report intervals.";
                RawText.Text = snapshot.RawBytes.Length == 0 ? "No report yet."
                    : string.Join("  ", snapshot.RawBytes.Select((b, i) => $"[{i}] {b:X2}"));
                if (_uiUpdateDelays.Count > 0)
                {
                    var sorted = _uiUpdateDelays.Order().ToArray();
                    var p95 = sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95) - 1)];
                    UiTimingText.Text = $"HID receipt → UI update: last {_uiUpdateDelays.Last():0.00} ms · p95 {p95:0.00} ms ({sorted.Length} recent state changes)";
                }
                else UiTimingText.Text = "Move a stick to measure HID receipt → UI update time.";
            }
        }
        if (XboxExpander.IsExpanded) XboxView.Update(_lastRenderedXbox);
    }

    private void RefreshXboxDisplay()
    {
        if (_xboxRead is null)
        {
            _xboxRead = Task.Run(ReadXboxStates);
            return;
        }
        if (!_xboxRead.IsCompleted) return;
        Dictionary<uint, GamepadState> states;
        try { states = _xboxRead.GetAwaiter().GetResult(); }
        catch (Exception ex)
        {
            XboxStatusText.Text = "Could not read Xbox input: " + ex.Message;
            XboxView.Update(GamepadState.Neutral);
            _xboxRead = null;
            return;
        }
        _xboxRead = Task.Run(ReadXboxStates);
        var selected = (uint)Math.Max(0, XboxSlotCombo.SelectedIndex);
        if (!states.ContainsKey(selected) && states.Count == 1)
        {
            selected = states.Keys.First();
            XboxSlotCombo.SelectedIndex = (int)selected;
        }
        var connected = states.TryGetValue(selected, out var active);
        XboxStatusText.Text = connected ? $"Xbox slot {selected + 1} is connected · {states.Count} connected"
            : $"Xbox slot {selected + 1} is disconnected · {states.Count} connected";
        _lastRenderedXbox = connected ? active : GamepadState.Neutral;
        XboxView.Update(_lastRenderedXbox, updateLabels: false);
        XboxView.Opacity = connected ? 1 : 0.45;
    }

    private Dictionary<uint, GamepadState> ReadXboxStates()
    {
        // Empty XInput slots can be expensive to probe. Retry them every two seconds off the UI thread.
        var states = new Dictionary<uint, GamepadState>();
        var now = Stopwatch.GetTimestamp();
        var probeMissing = now >= _nextXboxProbe;
        if (probeMissing) _nextXboxProbe = now + 2 * Stopwatch.Frequency;
        for (uint slot = 0; slot < 4; slot++)
        {
            if (!probeMissing && !_connectedXboxSlots.Contains(slot)) continue;
            if (XInputReader.TryRead(slot, out var state))
            {
                _connectedXboxSlots.Add(slot);
                states.Add(slot, state);
            }
            else _connectedXboxSlots.Remove(slot);
        }
        return states;
    }

    private static string ButtonName(XInputButtons button) => button switch
    {
        XInputButtons.LBumper => "LB", XInputButtons.RBumper => "RB",
        XInputButtons.Start => "Menu", XInputButtons.Back => "Back",
        XInputButtons.LeftStick => "L3", XInputButtons.RightStick => "R3",
        XInputButtons.DpadUp => "Up", XInputButtons.DpadDown => "Down",
        XInputButtons.DpadLeft => "Left", XInputButtons.DpadRight => "Right",
        _ => button.ToString(),
    };

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await ScanDevicesAsync();

    private async void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_scanning && !_closing && DeviceComboBox.SelectedItem is not null) await StartSelectedAsync();
    }

    private async void ListenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) { await StartSelectedAsync(); return; }
        await _sessionGate.WaitAsync();
        try
        {
            _manuallyStopped = true;
            ListenButton.IsEnabled = false;
            await StopCurrentAsync();
            StatusText.Text = "Input test stopped";
            StatusDetail.Text = "Choose Start test to listen again. No virtual controller was created.";
        }
        finally { if (!_closing) ListenButton.IsEnabled = true; _sessionGate.Release(); }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closeReady) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _refresh.Stop();
        if (_renderAttached)
        {
            CompositionTarget.Rendering -= RenderLatestInput;
            _renderAttached = false;
        }
        _devices.DeviceListChanged -= DeviceListChanged;
        await _sessionGate.WaitAsync();
        try { await StopCurrentAsync(); }
        finally
        {
            _devices.Dispose();
            _sessionGate.Release();
            _closeReady = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }
}
