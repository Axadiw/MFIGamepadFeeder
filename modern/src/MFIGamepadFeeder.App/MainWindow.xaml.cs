using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MFIGamepadFeeder.Core.Backends;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;
using MFIGamepadFeeder.Core.Session;

namespace MFIGamepadFeeder.App;

public partial class MainWindow : Window
{
    private const int MaxLogLengthChars = 64 * 1024;

    private readonly HidDeviceService _hidService = new();
    private readonly EventHandler _processExitHandler;
    private IVirtualGamepadBackend? _activeBackend;
    private GamepadFeederSession? _session;
    private bool _isStartingOrRunning;
    private bool _isSyncingSliders;

    public MainWindow()
    {
        InitializeComponent();

        SlotComboBox.ItemsSource = new uint[] { 1, 2, 3, 4 };
        SlotComboBox.SelectedIndex = 0;

        BackendComboBox.ItemsSource = new[]
        {
            BackendPreference.Auto,
            BackendPreference.ViGEmBus,
            BackendPreference.ScpVBus,
        };
        BackendComboBox.SelectedIndex = 0;

        _hidService.DeviceListChanged += OnHidDeviceListChanged;
        _processExitHandler = (_, _) => StopSessionSafe();
        AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshDriverBanner();
        RefreshMappingsList();
        RefreshDevicesList();
        AppendLog("Ready. Pair your SteelSeries Nimbus via Bluetooth, verify selection, and click Start Feeding.");
    }

    private void RefreshDriverBanner()
    {
        var probe = BackendFactory.Probe();
        DriverStatusText.Text = probe.Summary;

        if (probe.ViGEmAvailable)
        {
            DriverBanner.Background = new SolidColorBrush(Color.FromRgb(0xDA, 0xFB, 0xE1));
            DriverBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xC2, 0x6B));
        }
        else if (probe.ScpVBusAvailable)
        {
            DriverBanner.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xC5));
            DriverBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xA7, 0x2C));
        }
        else
        {
            DriverBanner.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xE9));
            DriverBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x81, 0x82));
        }
    }

    private void RefreshMappingsList(string? selectPath = null)
    {
        var configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        Directory.CreateDirectory(configsDir);

        var filePaths = Directory.GetFiles(configsDir, "*.mfimapping", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName)
            .ToList();

        if (!string.IsNullOrWhiteSpace(selectPath) && File.Exists(selectPath) &&
            !filePaths.Contains(selectPath, StringComparer.OrdinalIgnoreCase))
        {
            filePaths.Insert(0, selectPath);
        }

        var choices = filePaths
            .Select(p => MappingFileChoice.FromPath(p, configsDir))
            .ToList();

        var previousPath = selectPath ?? GetSelectedMappingPath();
        MappingComboBox.ItemsSource = choices;

        if (!string.IsNullOrWhiteSpace(previousPath))
        {
            var matched = choices.FirstOrDefault(c => string.Equals(c.FilePath, previousPath, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                MappingComboBox.SelectedItem = matched;
                return;
            }
        }

        var nimbusDefault = choices.FirstOrDefault(c =>
            Path.GetFileName(c.FilePath).Equals("Nimbus.mfimapping", StringComparison.OrdinalIgnoreCase));
        MappingComboBox.SelectedItem = nimbusDefault ?? choices.FirstOrDefault();
    }

    private string? GetSelectedMappingPath()
    {
        return MappingComboBox.SelectedItem switch
        {
            MappingFileChoice choice => choice.FilePath,
            string s => s,
            _ => null,
        };
    }

    private void MappingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var path = GetSelectedMappingPath();
        MappingComboBox.ToolTip = path;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var mapping = MappingSerializer.LoadFromFile(path);
            SyncSlidersFromMapping(mapping);

            if (mapping.MappingItems.Count == 0)
            {
                AppendLog($"Warning: '{Path.GetFileName(path)}' contains 0 MappingItems (empty config). Select Configs\\Nimbus.mfimapping for SteelSeries Nimbus.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Warning: Could not parse '{Path.GetFileName(path)}': {ex.Message}");
        }
    }

    private void SyncSlidersFromMapping(GamepadMapping mapping)
    {
        _isSyncingSliders = true;
        try
        {
            if (mapping.Deadzone.HasValue && double.IsFinite(mapping.Deadzone.Value))
            {
                var pct = Math.Clamp(Math.Round(mapping.Deadzone.Value * 100.0), DeadzoneSlider.Minimum, DeadzoneSlider.Maximum);
                DeadzoneSlider.Value = pct;
                DeadzoneValueText.Text = $"{(int)pct}%";
            }

            if (mapping.ButtonThreshold.HasValue)
            {
                var threshold = Math.Clamp((double)mapping.ButtonThreshold.Value, ThresholdSlider.Minimum, ThresholdSlider.Maximum);
                ThresholdSlider.Value = threshold;
                ThresholdValueText.Text = $"{(int)threshold}";
            }
        }
        finally
        {
            _isSyncingSliders = false;
        }
    }

    private void RefreshDevicesList()
    {
        var includeAll = ShowAllHidCheckBox.IsChecked == true;
        var devices = _hidService.GetCurrentDevices(includeAll);
        var previousPath = (DeviceComboBox.SelectedItem as HidDeviceInfo)?.DevicePath;

        DeviceComboBox.ItemsSource = devices;

        if (devices.Count == 0)
        {
            DeviceComboBox.SelectedItem = null;
            return;
        }

        var restored = previousPath is not null
            ? devices.FirstOrDefault(d => string.Equals(d.DevicePath, previousPath, StringComparison.OrdinalIgnoreCase))
            : null;

        DeviceComboBox.SelectedItem = restored
            ?? devices.FirstOrDefault(d => d.IsKnownMfi)
            ?? devices.FirstOrDefault(d => d.IsHidGamepadUsage)
            ?? devices[0];
    }

    private void OnHidDeviceListChanged()
    {
        InvokeOnUiIfAlive(() =>
        {
            if (!_isStartingOrRunning)
            {
                RefreshDevicesList();
            }
        });
    }

    private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceComboBox.SelectedItem is not HidDeviceInfo device || device.KnownProfile is null)
        {
            return;
        }

        // Auto-select recommended mapping file (e.g., Nimbus.mfimapping for 0x0111:0x1420, NimbusPlus.mfimapping for Nimbus+).
        if (MappingComboBox.ItemsSource is IEnumerable<MappingFileChoice> choices)
        {
            var match = choices.FirstOrDefault(c =>
                Path.GetFileName(c.FilePath).Equals(device.KnownProfile.RecommendedConfigFileName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                MappingComboBox.SelectedItem = match;
            }
        }
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStartingOrRunning)
        {
            StopSessionSafe();
            SetRunningUiState(false);
            AppendLog("Stopped.");
            return;
        }

        if (DeviceComboBox.SelectedItem is not HidDeviceInfo selectedDevice)
        {
            AppendLog("No HID gamepad selected. Turn on your SteelSeries Nimbus (Bluetooth) and click Refresh.");
            return;
        }

        var mappingPath = GetSelectedMappingPath();
        if (string.IsNullOrWhiteSpace(mappingPath) || !File.Exists(mappingPath))
        {
            AppendLog("Please select a valid .mfimapping file.");
            return;
        }

        var slot = SlotComboBox.SelectedItem is uint s ? s : 1u;
        var backendPref = BackendComboBox.SelectedItem is BackendPreference bp ? bp : BackendPreference.Auto;

        IVirtualController? virtualController = null;
        try
        {
            var mapping = MappingSerializer.LoadFromFile(mappingPath);
            if (mapping.MappingItems.Count == 0)
            {
                AppendLog($"Mapping '{Path.GetFileName(mappingPath)}' is empty (0 MappingItems). Select Configs\\Nimbus.mfimapping instead.");
                return;
            }

            var mapper = new HidReportMapper(mapping);
            _activeBackend = BackendFactory.Create(backendPref);
            virtualController = _activeBackend.ConnectController(slot);
            var hidSource = new HidSharpReportSource(selectedDevice);

            var session = new GamepadFeederSession(hidSource, mapper, virtualController, BuildRuntimeOptions());
            virtualController = null; // Ownership transferred to session.
            _session = session;

            _session.LogMessage += msg => InvokeOnUiIfAlive(() => AppendLog(msg));
            _session.TelemetryUpdated += telemetry => InvokeOnUiIfAlive(() => UpdateTelemetryUi(telemetry));
            _session.Start();

            SetRunningUiState(true);
            AppendLog($"Started slot {slot} using {_activeBackend.Name} with '{Path.GetFileName(mappingPath)}'.");
        }
        catch (Exception ex)
        {
            try { virtualController?.Dispose(); } catch { }
            StopSessionSafe();
            SetRunningUiState(false);
            AppendLog($"Start failed: {ex.Message}");
        }
    }

    private MappingRuntimeOptions BuildRuntimeOptions()
    {
        return new MappingRuntimeOptions(
            Deadzone: (DeadzoneSlider?.Value ?? 6.0) / 100.0,
            ButtonThreshold: (byte)Math.Clamp(ThresholdSlider?.Value ?? 1.0, 1.0, 255.0),
            InvertLeftY: InvertLeftYCheckBox?.IsChecked == true,
            InvertRightY: InvertRightYCheckBox?.IsChecked == true);
    }

    private void TuningControl_Changed(object sender, RoutedEventArgs e)
    {
        if (DeadzoneValueText is not null && DeadzoneSlider is not null)
        {
            DeadzoneValueText.Text = $"{(int)DeadzoneSlider.Value}%";
        }

        if (ThresholdValueText is not null && ThresholdSlider is not null)
        {
            ThresholdValueText.Text = $"{(int)ThresholdSlider.Value}";
        }

        if (!_isSyncingSliders)
        {
            _session?.UpdateRuntimeOptions(BuildRuntimeOptions());
        }
    }

    private void UpdateTelemetryUi(FeederTelemetry telemetry)
    {
        SessionStatusText.Text = $"Status: {telemetry.Status} ({telemetry.BackendName})";
        StatsText.Text = $"{telemetry.ReportsPerSecond:0.0} Hz | Submitted: {telemetry.SubmittedReports} | Skipped: {telemetry.SkippedReports} | Reconnects: {telemetry.ReconnectCount}";

        var s = telemetry.LastState;
        LeftStickText.Text = $"{s.LeftThumbX,6}, {s.LeftThumbY,6}";
        RightStickText.Text = $"{s.RightThumbX,6}, {s.RightThumbY,6}";
        LeftTriggerLabel.Text = $"Left Trigger: {s.LeftTrigger}";
        RightTriggerLabel.Text = $"Right Trigger: {s.RightTrigger}";
        LeftTriggerBar.Value = s.LeftTrigger;
        RightTriggerBar.Value = s.RightTrigger;
        ButtonsText.Text = $"Active Buttons: {(s.Buttons == XInputButtons.None ? "None" : s.Buttons.ToString())}";

        if (telemetry.LastRawBytes.Length > 0)
        {
            var formatted = string.Join("  ", telemetry.LastRawBytes.Select((b, idx) => $"[{idx}]={b}"));
            RawBytesText.Text = $"Raw HID ({telemetry.LastRawBytes.Length}B): {formatted}";
        }
        else if (telemetry.Status is FeederConnectionStatus.Stopped or FeederConnectionStatus.Faulted)
        {
            RawBytesText.Text = "Raw HID Bytes: (waiting for controller input)";
        }

        if (telemetry.Status == FeederConnectionStatus.Faulted && _isStartingOrRunning)
        {
            StopSessionSafe();
            SetRunningUiState(false);
            SessionStatusText.Text = $"Status: Faulted ({telemetry.BackendName})";
            RefreshDriverBanner();
        }
    }

    private void SetRunningUiState(bool running)
    {
        _isStartingOrRunning = running;
        DeviceComboBox.IsEnabled = !running;
        MappingComboBox.IsEnabled = !running;
        SlotComboBox.IsEnabled = !running;
        BackendComboBox.IsEnabled = !running;
        ShowAllHidCheckBox.IsEnabled = !running;
        RefreshDriversButton.IsEnabled = !running;
        RefreshDevicesButton.IsEnabled = !running;
        BrowseMappingButton.IsEnabled = !running;
        OpenEditorButton.IsEnabled = !running;

        StartStopButton.Content = running ? "Stop Feeding" : "Start Feeding";
        StartStopButton.Background = running
            ? new SolidColorBrush(Color.FromRgb(0xCF, 0x22, 0x2E))
            : new SolidColorBrush(Color.FromRgb(0x1F, 0x88, 0x3D));

        if (!running)
        {
            SessionStatusText.Text = "Status: Stopped";
        }
    }

    private void StopSessionSafe()
    {
        var session = Interlocked.Exchange(ref _session, null);
        try
        {
            session?.Dispose();
        }
        catch
        {
            // Ignore errors during shutdown cleanup.
        }

        var backend = Interlocked.Exchange(ref _activeBackend, null);
        try
        {
            backend?.Dispose();
        }
        catch
        {
            // Ignore errors during shutdown cleanup.
        }
    }

    private void InvokeOnUiIfAlive(Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        Dispatcher.BeginInvoke(action);
    }

    private void RefreshDriversButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStartingOrRunning)
        {
            return;
        }

        RefreshDriverBanner();
    }

    private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isStartingOrRunning)
        {
            RefreshDevicesList();
        }
    }

    private void ShowAllHidCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isStartingOrRunning)
        {
            RefreshDevicesList();
        }
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e) => LogTextBox.Clear();

    private void BrowseMappingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStartingOrRunning)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select MFI Mapping File",
            Filter = "MFI Mapping (*.mfimapping)|*.mfimapping|All Files (*.*)|*.*",
            InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs"),
        };

        if (dialog.ShowDialog(this) == true)
        {
            RefreshMappingsList(dialog.FileName);
        }
    }

    private void OpenEditorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStartingOrRunning)
        {
            return;
        }

        var editor = new MappingEditorWindow(
            _hidService,
            DeviceComboBox.SelectedItem as HidDeviceInfo,
            GetSelectedMappingPath())
        {
            Owner = this,
        };

        editor.ShowDialog();
        RefreshMappingsList(editor.LastSavedPath);
    }

    private void AppendLog(string message)
    {
        if (LogTextBox.Text.Length > MaxLogLengthChars)
        {
            LogTextBox.Text = LogTextBox.Text[^(MaxLogLengthChars / 2)..];
        }

        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
        _hidService.DeviceListChanged -= OnHidDeviceListChanged;
        StopSessionSafe();
        _hidService.Dispose();
    }

    private sealed record MappingFileChoice(string FilePath, string DisplayName)
    {
        public static MappingFileChoice FromPath(string path, string configsDir)
        {
            var fileName = Path.GetFileName(path);
            var fullDir = Path.GetFullPath(Path.GetDirectoryName(path) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar);
            var fullConfigs = Path.GetFullPath(configsDir).TrimEnd(Path.DirectorySeparatorChar);
            var isBundled = string.Equals(fullDir, fullConfigs, StringComparison.OrdinalIgnoreCase);

            var display = isBundled ? fileName : $"{fileName} ({path})";
            return new MappingFileChoice(path, display);
        }

        public override string ToString() => DisplayName;
    }
}
