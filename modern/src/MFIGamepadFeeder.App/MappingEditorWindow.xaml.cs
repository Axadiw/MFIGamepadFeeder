using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.App;

public sealed class NullableIntConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int i ? i.ToString(culture) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.Integer, culture, out var parsed)
            ? parsed
            : DependencyProperty.UnsetValue;
    }
}

public sealed class NullableByteConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is byte b ? b.ToString(culture) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return byte.TryParse(text, NumberStyles.Integer, culture, out var parsed)
            ? parsed
            : DependencyProperty.UnsetValue;
    }
}

public partial class MappingEditorWindow : Window
{
    private static readonly XInputButtons?[] ButtonOptions =
    [
        null,
        XInputButtons.A,
        XInputButtons.B,
        XInputButtons.X,
        XInputButtons.Y,
        XInputButtons.LBumper,
        XInputButtons.RBumper,
        XInputButtons.Start,
        XInputButtons.Back,
        XInputButtons.LeftStick,
        XInputButtons.RightStick,
        XInputButtons.Guide,
        XInputButtons.DpadUp,
        XInputButtons.DpadDown,
        XInputButtons.DpadLeft,
        XInputButtons.DpadRight,
    ];

    private static readonly AxisType?[] AxisOptions =
    [
        null,
        AxisType.Lx,
        AxisType.Ly,
        AxisType.Rx,
        AxisType.Ry,
        AxisType.LTrigger,
        AxisType.RTrigger,
    ];

    private readonly HidDeviceService _hidService;
    private readonly HidDeviceInfo? _initialDevice;
    private readonly string? _initialMappingPath;

    private readonly ObservableCollection<EditableMappingRow> _rows = [];
    private readonly ObservableCollection<EditableVirtualKeyRow> _virtualKeys = [];
    private string? _currentFilePath;

    private readonly object _previewLock = new();
    private CancellationTokenSource? _previewCts;
    private IHidReportStream? _previewStream;
    private bool _isLoadingRows;

    public string? LastSavedPath { get; private set; }

    public MappingEditorWindow(HidDeviceService hidService, HidDeviceInfo? initialDevice, string? initialMappingPath)
    {
        InitializeComponent();
        _hidService = hidService;
        _initialDevice = initialDevice;
        _initialMappingPath = initialMappingPath;

        TypeColumn.ItemsSource = Enum.GetValues<GamepadMappingItemType>();
        ButtonColumn.ItemsSource = ButtonOptions;
        AxisColumn.ItemsSource = AxisOptions;

        VkSource1Column.ItemsSource = ButtonOptions;
        VkSource2Column.ItemsSource = ButtonOptions;
        VkSource3Column.ItemsSource = ButtonOptions;
        VkSource4Column.ItemsSource = ButtonOptions;
        VkDestinationColumn.ItemsSource = ButtonOptions;

        _rows.CollectionChanged += Rows_CollectionChanged;
        ItemsDataGrid.ItemsSource = _rows;
        VirtualKeysDataGrid.ItemsSource = _virtualKeys;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var devices = _hidService.GetCurrentDevices(includeAllHid: true);
        PreviewDeviceComboBox.ItemsSource = devices;
        if (_initialDevice is not null)
        {
            PreviewDeviceComboBox.SelectedItem = devices.FirstOrDefault(d =>
                string.Equals(d.DevicePath, _initialDevice.DevicePath, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_initialMappingPath) && File.Exists(_initialMappingPath))
        {
            LoadMappingFromPath(_initialMappingPath);
        }
    }

    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_isLoadingRows)
        {
            ReindexRows();
        }
    }

    private void ReindexRows()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].RowIndex = i;
        }
    }

    private void LoadMappingFromPath(string path)
    {
        try
        {
            var mapping = MappingSerializer.LoadFromFile(path);
            _currentFilePath = path;
            LoadMapping(mapping, path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open mapping file: {ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadMapping(GamepadMapping mapping, string label)
    {
        _isLoadingRows = true;
        try
        {
            _rows.Clear();
            for (var i = 0; i < mapping.MappingItems.Count; i++)
            {
                var item = mapping.MappingItems[i];
                if (item is null)
                {
                    continue;
                }

                _rows.Add(new EditableMappingRow
                {
                    RowIndex = _rows.Count,
                    Type = item.Type,
                    ButtonType = item.ButtonType,
                    AxisType = item.AxisType,
                    ConvertAxis = item.ConvertAxis ?? false,
                    UnsignedCentered = item.UnsignedCentered ?? false,
                    InvertAxis = item.InvertAxis ?? false,
                    CustomIndex = item.CustomIndex,
                    CustomValue = item.CustomValue,
                    Threshold = item.Threshold,
                });
            }
        }
        finally
        {
            _isLoadingRows = false;
        }

        _virtualKeys.Clear();
        foreach (var vk in mapping.VirtualKeysItems)
        {
            if (vk is null)
            {
                continue;
            }

            var sources = (vk.SourceKeys ?? []).Where(k => k.HasValue).ToList();
            _virtualKeys.Add(new EditableVirtualKeyRow
            {
                SourceKey1 = sources.Count > 0 ? sources[0] : null,
                SourceKey2 = sources.Count > 1 ? sources[1] : null,
                SourceKey3 = sources.Count > 2 ? sources[2] : null,
                SourceKey4 = sources.Count > 3 ? sources[3] : null,
                Destination = vk.DestinationItem,
            });
        }

        ReportIdTextBox.Text = mapping.ReportId.HasValue ? mapping.ReportId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        DeadzonePctTextBox.Text = mapping.Deadzone.HasValue
            ? Math.Round(mapping.Deadzone.Value * 100.0, 2).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        ButtonThresholdTextBox.Text = mapping.ButtonThreshold.HasValue
            ? mapping.ButtonThreshold.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        FilePathText.Text = label;
    }

    internal GamepadMapping BuildMapping(out string? validationError)
    {
        CommitAllEdits();
        validationError = null;

        byte? reportId = null;
        if (!string.IsNullOrWhiteSpace(ReportIdTextBox.Text))
        {
            if (!byte.TryParse(ReportIdTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                validationError = "Report ID must be an integer between 0 and 255 (or blank).";
                return new GamepadMapping();
            }

            reportId = parsedId;
        }

        double? deadzone = null;
        if (!string.IsNullOrWhiteSpace(DeadzonePctTextBox.Text))
        {
            if (!double.TryParse(DeadzonePctTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedPct) ||
                !double.IsFinite(parsedPct) || parsedPct < 0.0 || parsedPct > 50.0)
            {
                validationError = "Default Deadzone (%) must be a number between 0 and 50 (or blank).";
                return new GamepadMapping();
            }

            deadzone = parsedPct / 100.0;
        }

        byte? buttonThreshold = null;
        if (!string.IsNullOrWhiteSpace(ButtonThresholdTextBox.Text))
        {
            if (!byte.TryParse(ButtonThresholdTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTh) ||
                parsedTh < 1)
            {
                validationError = "Default Button Threshold must be an integer between 1 and 255 (or blank).";
                return new GamepadMapping();
            }

            buttonThreshold = parsedTh;
        }

        var items = new List<GamepadMappingItem>(_rows.Count);
        for (var i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r.CustomIndex.HasValue && r.CustomIndex.Value < 0)
            {
                validationError = $"Row {i}: CustomIndex cannot be negative.";
                return new GamepadMapping();
            }

            if (r.CustomValue.HasValue && (r.CustomValue.Value < 1 || r.CustomValue.Value > 255))
            {
                validationError = $"Row {i}: CustomMask must be between 1 and 255 (or blank).";
                return new GamepadMapping();
            }

            if (r.Threshold.HasValue && r.Threshold.Value < 1)
            {
                validationError = $"Row {i}: Threshold must be between 1 and 255 (or blank).";
                return new GamepadMapping();
            }

            items.Add(new GamepadMappingItem
            {
                Type = r.Type,
                ButtonType = r.ButtonType,
                AxisType = r.AxisType,
                ConvertAxis = r.ConvertAxis,
                UnsignedCentered = r.UnsignedCentered ? true : null,
                InvertAxis = r.InvertAxis,
                CustomIndex = r.CustomIndex,
                CustomValue = r.CustomValue,
                Threshold = r.Threshold,
            });
        }

        var virtualKeys = new List<VirtualKeyMappingItem>();
        foreach (var vk in _virtualKeys)
        {
            var sources = new[] { vk.SourceKey1, vk.SourceKey2, vk.SourceKey3, vk.SourceKey4 }
                .Where(k => k.HasValue)
                .Distinct()
                .ToList();

            if (sources.Count > 0 && vk.Destination.HasValue)
            {
                virtualKeys.Add(new VirtualKeyMappingItem
                {
                    SourceKeys = sources,
                    DestinationItem = vk.Destination,
                });
            }
        }

        return new GamepadMapping
        {
            ReportId = reportId,
            Deadzone = deadzone,
            ButtonThreshold = buttonThreshold,
            MappingItems = items,
            VirtualKeysItems = virtualKeys,
        };
    }

    private void CommitAllEdits()
    {
        ItemsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ItemsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        VirtualKeysDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        VirtualKeysDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    // FIX: Unlike legacy MappingEditorWindow.NewButton_Click, clicking New does NOT prematurely
    // open SaveFileDialog and overwrite a file with an empty {"MappingItems":[],"VirtualKeysItems":[]}!
    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        _currentFilePath = null;
        LoadMapping(new GamepadMapping(), "New unsaved mapping");
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        Directory.CreateDirectory(configsDir);

        var dialog = new OpenFileDialog
        {
            InitialDirectory = configsDir,
            Filter = "MFI Mapping (*.mfimapping)|*.mfimapping",
        };

        if (dialog.ShowDialog(this) == true)
        {
            LoadMappingFromPath(dialog.FileName);
        }
    }

    private void SaveDirectButton_Click(object sender, RoutedEventArgs e)
    {
        SaveMapping(promptForPath: string.IsNullOrWhiteSpace(_currentFilePath));
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveMapping(promptForPath: true);
    }

    private void SaveMapping(bool promptForPath)
    {
        var mapping = BuildMapping(out var validationError);
        if (validationError is not null)
        {
            MessageBox.Show(this, validationError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (mapping.MappingItems.Count == 0)
        {
            var confirm = MessageBox.Show(
                this,
                "This mapping currently has 0 items and will not produce any gamepad input. Save anyway?",
                "Empty Mapping Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var targetPath = _currentFilePath;
        if (promptForPath || string.IsNullOrWhiteSpace(targetPath))
        {
            var configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
            Directory.CreateDirectory(configsDir);

            var dialog = new SaveFileDialog
            {
                InitialDirectory = !string.IsNullOrWhiteSpace(_currentFilePath)
                    ? Path.GetDirectoryName(_currentFilePath) ?? configsDir
                    : configsDir,
                Filter = "MFI Mapping (*.mfimapping)|*.mfimapping",
                DefaultExt = ".mfimapping",
                FileName = !string.IsNullOrWhiteSpace(_currentFilePath)
                    ? Path.GetFileName(_currentFilePath)
                    : "CustomNimbus.mfimapping",
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            targetPath = dialog.FileName;
        }

        try
        {
            MappingSerializer.SaveToFile(targetPath, mapping);
            _currentFilePath = targetPath;
            LastSavedPath = targetPath;
            FilePathText.Text = targetPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save mapping: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddRowButton_Click(object sender, RoutedEventArgs e)
    {
        CommitAllEdits();
        _rows.Add(new EditableMappingRow { RowIndex = _rows.Count, Type = GamepadMappingItemType.Empty });
    }

    private void RemoveRowButton_Click(object sender, RoutedEventArgs e)
    {
        CommitAllEdits();
        if (ItemsDataGrid.SelectedItem is EditableMappingRow row)
        {
            _rows.Remove(row);
        }
    }

    private void AddVirtualKeyButton_Click(object sender, RoutedEventArgs e)
    {
        CommitAllEdits();
        _virtualKeys.Add(new EditableVirtualKeyRow());
    }

    private void RemoveVirtualKeyButton_Click(object sender, RoutedEventArgs e)
    {
        CommitAllEdits();
        if (VirtualKeysDataGrid.SelectedItem is EditableVirtualKeyRow vk)
        {
            _virtualKeys.Remove(vk);
        }
    }

    private void PreviewDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StopPreview();
        if (PreviewDeviceComboBox.SelectedItem is not HidDeviceInfo device)
        {
            LiveBytesText.Text = "Waiting for HID input...";
            return;
        }

        var cts = new CancellationTokenSource();
        CancellationToken ct;
        lock (_previewLock)
        {
            _previewCts = cts;
            ct = cts.Token;
        }

        var source = new HidSharpReportSource(device);

        _ = Task.Run(() =>
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (!source.TryOpen(out var stream, out var err) || stream is null)
            {
                if (!ct.IsCancellationRequested)
                {
                    InvokePreviewText($"Could not open preview stream: {err}");
                }
                return;
            }

            lock (_previewLock)
            {
                if (ct.IsCancellationRequested || !ReferenceEquals(_previewCts, cts))
                {
                    stream.Dispose();
                    return;
                }

                _previewStream = stream;
            }

            try
            {
                using (stream)
                {
                    var buf = new byte[Math.Max(64, stream.MaxInputReportLength)];
                    byte[]? prevSnapshot = null;
                    var uiThrottle = Stopwatch.StartNew();
                    var pendingUpdate = false;
                    var lastCount = 0;

                    while (!ct.IsCancellationRequested)
                    {
                        var res = stream.Read(buf, ct);
                        if (res.Kind == HidReadResultKind.Data)
                        {
                            lastCount = res.BytesRead;
                            if (uiThrottle.ElapsedMilliseconds >= 50 || prevSnapshot is null)
                            {
                                var formatted = FormatBytesWithDiff(buf, lastCount, prevSnapshot);
                                prevSnapshot = buf[..lastCount].ToArray();
                                pendingUpdate = false;
                                uiThrottle.Restart();
                                InvokePreviewText(formatted);
                            }
                            else
                            {
                                pendingUpdate = true;
                            }
                        }
                        else if (res.Kind == HidReadResultKind.Timeout)
                        {
                            if (pendingUpdate && lastCount > 0 && uiThrottle.ElapsedMilliseconds >= 50)
                            {
                                var formatted = FormatBytesWithDiff(buf, lastCount, prevSnapshot);
                                prevSnapshot = buf[..lastCount].ToArray();
                                pendingUpdate = false;
                                uiThrottle.Restart();
                                InvokePreviewText(formatted);
                            }
                        }
                        else if (res.Kind == HidReadResultKind.Disconnected)
                        {
                            if (!ct.IsCancellationRequested)
                            {
                                InvokePreviewText("Preview device disconnected.");
                            }
                            break;
                        }
                    }
                }
            }
            finally
            {
                lock (_previewLock)
                {
                    if (ReferenceEquals(_previewStream, stream))
                    {
                        _previewStream = null;
                    }
                }
            }
        }, CancellationToken.None);
    }

    private static string FormatBytesWithDiff(byte[] current, int length, byte[]? previous)
    {
        var parts = new string[length];
        for (var i = 0; i < length; i++)
        {
            var changed = previous is not null && i < previous.Length && previous[i] != current[i];
            parts[i] = changed ? $"[{i}]={current[i]}*" : $"[{i}]={current[i]}";
        }

        return $"({length}B) {string.Join("  ", parts)}";
    }

    private void InvokePreviewText(string text)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => LiveBytesText.Text = text);
    }

    private void StopPreview()
    {
        CancellationTokenSource? cts;
        IHidReportStream? stream;
        lock (_previewLock)
        {
            cts = _previewCts;
            stream = _previewStream;
            _previewCts = null;
            _previewStream = null;
        }

        try { cts?.Cancel(); } catch { }
        try { stream?.Dispose(); } catch { }
        try { cts?.Dispose(); } catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        StopPreview();
    }

    private sealed class EditableMappingRow : INotifyPropertyChanged
    {
        private int _rowIndex;
        private GamepadMappingItemType _type;
        private XInputButtons? _buttonType;
        private AxisType? _axisType;
        private bool _convertAxis;
        private bool _unsignedCentered;
        private bool _invertAxis;
        private int? _customIndex;
        private int? _customValue;
        private byte? _threshold;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int RowIndex
        {
            get => _rowIndex;
            set => SetField(ref _rowIndex, value);
        }

        public GamepadMappingItemType Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        public XInputButtons? ButtonType
        {
            get => _buttonType;
            set => SetField(ref _buttonType, value);
        }

        public AxisType? AxisType
        {
            get => _axisType;
            set => SetField(ref _axisType, value);
        }

        public bool ConvertAxis
        {
            get => _convertAxis;
            set => SetField(ref _convertAxis, value);
        }

        public bool UnsignedCentered
        {
            get => _unsignedCentered;
            set => SetField(ref _unsignedCentered, value);
        }

        public bool InvertAxis
        {
            get => _invertAxis;
            set => SetField(ref _invertAxis, value);
        }

        public int? CustomIndex
        {
            get => _customIndex;
            set => SetField(ref _customIndex, value);
        }

        public int? CustomValue
        {
            get => _customValue;
            set => SetField(ref _customValue, value);
        }

        public byte? Threshold
        {
            get => _threshold;
            set => SetField(ref _threshold, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    private sealed class EditableVirtualKeyRow : INotifyPropertyChanged
    {
        private XInputButtons? _sourceKey1;
        private XInputButtons? _sourceKey2;
        private XInputButtons? _sourceKey3;
        private XInputButtons? _sourceKey4;
        private XInputButtons? _destination;

        public event PropertyChangedEventHandler? PropertyChanged;

        public XInputButtons? SourceKey1
        {
            get => _sourceKey1;
            set => SetField(ref _sourceKey1, value);
        }

        public XInputButtons? SourceKey2
        {
            get => _sourceKey2;
            set => SetField(ref _sourceKey2, value);
        }

        public XInputButtons? SourceKey3
        {
            get => _sourceKey3;
            set => SetField(ref _sourceKey3, value);
        }

        public XInputButtons? SourceKey4
        {
            get => _sourceKey4;
            set => SetField(ref _sourceKey4, value);
        }

        public XInputButtons? Destination
        {
            get => _destination;
            set => SetField(ref _destination, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
