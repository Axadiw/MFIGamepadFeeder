using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MFIGamepadFeeder.App;
using MFIGamepadFeeder.Core.Backends;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;
using MFIGamepadFeeder.Core.Session;
using Xunit;
using Xunit.Abstractions;

namespace MFIGamepadFeeder.Tests;

public sealed class ReviewAuditTests
{
    private readonly ITestOutputHelper _output;

    public ReviewAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ScpVBus_FeedbackBufferSize_MatchesDriver10ByteRumbleLedStatusContract()
    {
        // ScpVBus/bus/busenum.h: RUMBLE_SIZE(8) + LEDNUM_SIZE(1) + STATUS_SIZE(1) = 10
        // ScpVBus/bus/busenum.c:377 sets Irp->IoStatus.Information = 10 unconditionally!
        Assert.Equal(10, ScpVBusPacketBuilder.FeedbackBufferSize);
        Assert.Equal(9, ScpVBusPacketBuilder.FeedbackStartedStatusOffset);

        var notStarted = new byte[10];
        Assert.False(ScpVBusPacketBuilder.IsFeedbackDeviceStarted(notStarted));
        notStarted[9] = 1;
        Assert.True(ScpVBusPacketBuilder.IsFeedbackDeviceStarted(notStarted));
        Assert.False(ScpVBusPacketBuilder.IsFeedbackDeviceStarted(new byte[9]));

        var isPluggedBuf = new byte[ScpVBusPacketBuilder.IsDevPluggedBufferSize];
        ScpVBusPacketBuilder.BuildIsDevPluggedInput(3, isPluggedBuf);
        Assert.Equal(3u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(isPluggedBuf));
    }

    [Fact]
    public void ScpVBus_LiveReadOnlyIsDevPluggedProbe_SucceedsWhenDriverPresent()
    {
        var probe = BackendFactory.Probe();
        if (!probe.ScpVBusAvailable)
        {
            _output.WriteLine("ScpVBus not available on host; skipping live read-only slot query.");
            return;
        }

        for (uint slot = 1; slot <= 4; slot++)
        {
            var ok = ScpVBusBackend.TryQuerySlotPlugged(slot, out var plugged);
            _output.WriteLine($"Live ScpVBus ISDEVPLUGGED slot {slot}: ok={ok}, plugged={plugged}");
            Assert.True(ok, $"Expected live read-only IOCTL_BUSENUM_ISDEVPLUGGED to succeed for slot {slot}.");
        }
    }

    [Fact]
    public void HidReportMapper_HandlesNullEntriesInJsonArraysAndNaNDeadzoneSafely()
    {
        var json = """
        {
          "MappingItems": [
            null,
            { "Type": 1, "ButtonType": 4096 },
            { "Type": 0, "AxisType": 2, "ConvertAxis": true }
          ],
          "VirtualKeysItems": [
            null,
            { "SourceKeys": [4096], "DestinationItem": 16 }
          ]
        }
        """;

        var mapping = MappingSerializer.Deserialize(json);
        var mapper = new HidReportMapper(mapping);

        byte[] report = [0, 255, 64];
        var ok = mapper.TryMapReport(report, out var state, out var diag, new MappingRuntimeOptions(Deadzone: double.NaN));

        Assert.True(ok);
        Assert.Null(diag);
        Assert.Equal(XInputButtons.Start, state.Buttons);
        Assert.True(state.LeftThumbX > 15000, $"Expected positive LX even with NaN deadzone fallback, got {state.LeftThumbX}");
        Assert.Equal(0.0, HidReportMapper.ApplyDeadzone(double.NaN, 0.06));
        Assert.Equal((short)0, HidReportMapper.ToInt16Stick(double.NaN));
    }

    [Fact]
    public async Task Session_UpdateRuntimeOptions_ReevaluatesLastStateEvenWhenControllerIsIdle()
    {
        var mapping = new GamepadMapping
        {
            MappingItems =
            [
                new GamepadMappingItem { Type = GamepadMappingItemType.Empty },
                new GamepadMappingItem { Type = GamepadMappingItemType.Axis, AxisType = AxisType.Ly, ConvertAxis = true },
            ],
        };
        var mapper = new HidReportMapper(mapping);
        var controller = new RecordingController();

        // Sends ONE packet (LY = +127), then goes completely idle (Timeouts only, like Bluetooth HID when stick is held steady).
        var stream = new SinglePacketThenIdleStream([0, 127]);
        var source = new SingleSource(stream);

        var firstSubmitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invertedSubmitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        controller.OnSubmit += s =>
        {
            if (s.LeftThumbY == short.MaxValue) firstSubmitted.TrySetResult(true);
            if (s.LeftThumbY == short.MinValue) invertedSubmitted.TrySetResult(true);
        };

        using var session = new GamepadFeederSession(
            source,
            mapper,
            controller,
            new MappingRuntimeOptions(Deadzone: 0.0, InvertLeftY: false),
            reconnectDelayMs: 20);

        session.Start();
        Assert.Same(firstSubmitted.Task, await Task.WhenAny(firstSubmitted.Task, Task.Delay(2000)));

        // Toggle InvertLeftY while NO new HID packet arrives!
        session.UpdateRuntimeOptions(new MappingRuntimeOptions(Deadzone: 0.0, InvertLeftY: true));

        var done = await Task.WhenAny(invertedSubmitted.Task, Task.Delay(1000));
        session.Stop();

        Assert.Same(invertedSubmitted.Task, done);
    }

    [Fact]
    public async Task Session_FastButtonTapRelease_DoesNotLeaveTelemetryRawBytesStuckOnPressed()
    {
        var mapping = new GamepadMapping
        {
            MappingItems =
            [
                new GamepadMappingItem { Type = GamepadMappingItemType.Empty },
                new GamepadMappingItem { Type = GamepadMappingItemType.Button, ButtonType = XInputButtons.A },
            ],
        };
        var mapper = new HidReportMapper(mapping);
        var controller = new RecordingController();

        // Packet 1: A pressed ([1]=255). Packet 2 immediately (<1ms later): A released ([1]=0). Then idle!
        var stream = new TwoPacketsThenIdleStream([0, 255], [0, 0]);
        var source = new SingleSource(stream);

        FeederTelemetry? latestTelemetry = null;
        var releasedRawSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var session = new GamepadFeederSession(
            source,
            mapper,
            controller,
            new MappingRuntimeOptions(Deadzone: 0.0),
            reconnectDelayMs: 20);

        session.TelemetryUpdated += t =>
        {
            latestTelemetry = t;
            if (t.SubmittedReports >= 2 && t.LastState.Buttons == XInputButtons.None &&
                t.LastRawBytes.Length == 2 && t.LastRawBytes[1] == 0)
            {
                releasedRawSeen.TrySetResult(true);
            }
        };

        session.Start();
        var completed = await Task.WhenAny(releasedRawSeen.Task, Task.Delay(1200));
        session.Stop();

        Assert.True(
            ReferenceEquals(releasedRawSeen.Task, completed),
            $"Telemetry raw bytes remained stale after quick button release: LastState={latestTelemetry?.LastState}, LastRaw=[{string.Join(",", latestTelemetry?.LastRawBytes ?? [])}]");
    }

    [Fact]
    public async Task Session_FatalVirtualControllerFailure_EmitsFaultedAndSupportsConcurrentStop()
    {
        var mapping = new GamepadMapping
        {
            MappingItems =
            [
                new GamepadMappingItem { Type = GamepadMappingItemType.Empty },
                new GamepadMappingItem { Type = GamepadMappingItemType.Button, ButtonType = XInputButtons.A },
            ],
        };
        var mapper = new HidReportMapper(mapping);
        var controller = new FailingController();
        var stream = new SinglePacketThenIdleStream([0, 255]);
        var source = new SingleSource(stream);

        var faultedSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var session = new GamepadFeederSession(source, mapper, controller, reconnectDelayMs: 20);
        session.TelemetryUpdated += t =>
        {
            if (t.Status == FeederConnectionStatus.Faulted)
            {
                faultedSeen.TrySetResult(true);
            }
        };

        session.Start();
        Assert.Same(faultedSeen.Task, await Task.WhenAny(faultedSeen.Task, Task.Delay(2000)));

        // Calling Stop() concurrently from multiple threads must never throw ObjectDisposedException.
        var stops = Enumerable.Range(0, 4).Select(_ => Task.Run(session.Stop)).ToArray();
        await Task.WhenAll(stops);
    }

    [Fact]
    public void MappingEditorWindow_WpfSta_PreservesUnsignedCenteredAndVirtualKeys_AndReindexesOnDelete()
    {
        RunOnStaThread(() =>
        {
            using var hid = new HidDeviceService();
            var tempInput = Path.Combine(Path.GetTempPath(), $"mfi_audit_in_{Guid.NewGuid():N}.mfimapping");
            try
            {
                var inputMapping = new GamepadMapping
                {
                    ReportId = 1,
                    Deadzone = 0.09,
                    ButtonThreshold = 15,
                    MappingItems =
                    [
                        new GamepadMappingItem { Type = GamepadMappingItemType.Empty },
                        new GamepadMappingItem { Type = GamepadMappingItemType.Axis, AxisType = AxisType.Lx, UnsignedCentered = true, InvertAxis = true },
                        new GamepadMappingItem { Type = GamepadMappingItemType.Button, ButtonType = XInputButtons.A, CustomIndex = 7, CustomValue = 16, Threshold = 20 },
                    ],
                    VirtualKeysItems =
                    [
                        new VirtualKeyMappingItem { SourceKeys = [XInputButtons.LBumper, XInputButtons.Start], DestinationItem = XInputButtons.LeftStick },
                    ],
                };
                MappingSerializer.SaveToFile(tempInput, inputMapping);

                var editor = new MappingEditorWindow(hid, initialDevice: null, initialMappingPath: tempInput);
                editor.Show();
                PumpDispatcher();

                var grid = (DataGrid)editor.FindName("ItemsDataGrid");
                var vkGrid = (DataGrid)editor.FindName("VirtualKeysDataGrid");
                Assert.NotNull(grid);
                Assert.NotNull(vkGrid);
                Assert.Equal(3, grid.Items.Count);
                Assert.Single(vkGrid.Items);

                // 1) Verify UnsignedCentered, VirtualKeys, ReportId, Deadzone, ButtonThreshold round-trip cleanly!
                var built = editor.BuildMapping(out var validationErr);
                Assert.Null(validationErr);
                Assert.Equal((byte)1, built.ReportId);
                Assert.Equal(0.09, built.Deadzone);
                Assert.Equal((byte)15, built.ButtonThreshold);
                Assert.Equal(true, built.MappingItems[1].UnsignedCentered);
                Assert.Equal(true, built.MappingItems[1].InvertAxis);
                Assert.Single(built.VirtualKeysItems);
                Assert.Equal(XInputButtons.LeftStick, built.VirtualKeysItems[0].DestinationItem);
                Assert.Equal([XInputButtons.LBumper, XInputButtons.Start], built.VirtualKeysItems[0].SourceKeys);

                // 2) Clear CustomIndex to whitespace "   " in DataGrid edit mode -> becomes null cleanly!
                var customIndexCol = grid.Columns.First(c => Equals(c.Header, "CustomIndex"));
                grid.SelectedIndex = 2;
                grid.CurrentCell = new DataGridCellInfo(grid.Items[2], customIndexCol);
                grid.BeginEdit();
                PumpDispatcher();

                var cellTextBox = customIndexCol.GetCellContent(grid.Items[2]) as TextBox;
                Assert.NotNull(cellTextBox);
                cellTextBox!.Text = "   ";

                // BuildMapping must commit BOTH Cell and Row edits before reading _rows!
                var builtAfterClear = editor.BuildMapping(out validationErr);
                Assert.Null(validationErr);
                Assert.Null(builtAfterClear.MappingItems[2].CustomIndex);

                // 3) Simulate user pressing Delete on row 0 (removes directly from ObservableCollection via CanUserDeleteRows="True")
                var rowsField = typeof(MappingEditorWindow).GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic);
                var rowsList = (System.Collections.IList)rowsField!.GetValue(editor)!;
                rowsList.RemoveAt(0);
                PumpDispatcher();

                var newFirstRowIndex = (int)grid.Items[0].GetType().GetProperty("RowIndex")!.GetValue(grid.Items[0])!;
                var newSecondRowIndex = (int)grid.Items[1].GetType().GetProperty("RowIndex")!.GetValue(grid.Items[1])!;
                Assert.Equal(0, newFirstRowIndex);
                Assert.Equal(1, newSecondRowIndex);

                // 4) Verify invalid metadata input produces clear validation error instead of crashing
                ((TextBox)editor.FindName("ReportIdTextBox")).Text = "999";
                editor.BuildMapping(out var badIdErr);
                Assert.NotNull(badIdErr);
                ((TextBox)editor.FindName("ReportIdTextBox")).Text = "";

                // 5) Verify converters reject non-numeric strings cleanly
                var intConv = new NullableIntConverter();
                Assert.Null(intConv.ConvertBack("  ", typeof(int?), null, System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(DependencyProperty.UnsetValue, intConv.ConvertBack("abc", typeof(int?), null, System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(7, intConv.ConvertBack(" 7 ", typeof(int?), null, System.Globalization.CultureInfo.InvariantCulture));

                var byteConv = new NullableByteConverter();
                Assert.Null(byteConv.ConvertBack("", typeof(byte?), null, System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(DependencyProperty.UnsetValue, byteConv.ConvertBack("999", typeof(byte?), null, System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal((byte)20, byteConv.ConvertBack("20", typeof(byte?), null, System.Globalization.CultureInfo.InvariantCulture));

                editor.Close();
                PumpDispatcher();
            }
            finally
            {
                if (File.Exists(tempInput)) File.Delete(tempInput);
            }
        });
    }

    [Fact]
    public void MainWindow_WpfSta_DisablesDriverAndDeviceControlsWhileRunning_AndSyncsMappingDefaults()
    {
        RunOnStaThread(() =>
        {
            var win = new MainWindow();
            win.Show();
            PumpDispatcher();

            // Verify SetRunningUiState(true) disables ALL controls that could corrupt state or trigger ScpVBus Bus_CleanUp
            var setRunning = typeof(MainWindow).GetMethod("SetRunningUiState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(setRunning);

            setRunning!.Invoke(win, [true]);
            Assert.False(((Button)win.FindName("RefreshDriversButton")).IsEnabled);
            Assert.False(((Button)win.FindName("RefreshDevicesButton")).IsEnabled);
            Assert.False(((Button)win.FindName("BrowseMappingButton")).IsEnabled);
            Assert.False(((Button)win.FindName("OpenEditorButton")).IsEnabled);

            setRunning.Invoke(win, [false]);
            Assert.True(((Button)win.FindName("RefreshDriversButton")).IsEnabled);
            Assert.True(((Button)win.FindName("RefreshDevicesButton")).IsEnabled);
            Assert.True(((Button)win.FindName("BrowseMappingButton")).IsEnabled);
            Assert.True(((Button)win.FindName("OpenEditorButton")).IsEnabled);

            // Verify selecting a mapping file with custom Deadzone/ButtonThreshold updates the sliders
            var tempMap = Path.Combine(Path.GetTempPath(), $"mfi_audit_sync_{Guid.NewGuid():N}.mfimapping");
            try
            {
                MappingSerializer.SaveToFile(tempMap, new GamepadMapping
                {
                    Deadzone = 0.14,
                    ButtonThreshold = 35,
                    MappingItems = [new GamepadMappingItem { Type = GamepadMappingItemType.Button, ButtonType = XInputButtons.A }],
                });

                var refreshMappings = typeof(MainWindow).GetMethod("RefreshMappingsList", BindingFlags.Instance | BindingFlags.NonPublic);
                refreshMappings!.Invoke(win, [tempMap]);
                PumpDispatcher();

                var dzSlider = (Slider)win.FindName("DeadzoneSlider");
                var thSlider = (Slider)win.FindName("ThresholdSlider");
                Assert.Equal(14.0, dzSlider.Value);
                Assert.Equal(35.0, thSlider.Value);
            }
            finally
            {
                if (File.Exists(tempMap)) File.Delete(tempMap);
            }

            win.Close();
            PumpDispatcher();
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(_ => { frame.Continue = false; return null; }),
            null);
        Dispatcher.PushFrame(frame);
    }

    private sealed class RecordingController : IVirtualController
    {
        public string BackendName => "Recording";
        public uint SlotIndex => 1;
        public event Action<GamepadState>? OnSubmit;
        public void SubmitState(in GamepadState state) => OnSubmit?.Invoke(state);
        public void Reset() { }
        public void Dispose() { }
    }

    private sealed class FailingController : IVirtualController
    {
        public string BackendName => "FailingBackend";
        public uint SlotIndex => 1;
        public void SubmitState(in GamepadState state) => throw new InvalidOperationException("Simulated bus failure");
        public void Reset() => throw new InvalidOperationException("Simulated bus reset failure");
        public void Dispose() { }
    }

    private sealed class SingleSource(IHidReportStream stream) : IHidReportSource
    {
        private bool _opened;
        public string DisplayName => "TestDevice";
        public bool TryOpen(out IHidReportStream? s, out string? error)
        {
            if (_opened) { s = null; error = "Closed"; return false; }
            _opened = true;
            s = stream;
            error = null;
            return true;
        }
    }

    private sealed class SinglePacketThenIdleStream(byte[] packet) : IHidReportStream
    {
        private bool _sent;
        public int MaxInputReportLength => packet.Length;
        public HidReadResult Read(byte[] buffer, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return new HidReadResult(HidReadResultKind.Disconnected, 0);
            if (!_sent)
            {
                _sent = true;
                Array.Copy(packet, buffer, packet.Length);
                return new HidReadResult(HidReadResultKind.Data, packet.Length);
            }
            try { Task.Delay(30, ct).GetAwaiter().GetResult(); } catch { }
            return ct.IsCancellationRequested
                ? new HidReadResult(HidReadResultKind.Disconnected, 0)
                : new HidReadResult(HidReadResultKind.Timeout, 0);
        }
        public void Dispose() { }
    }

    private sealed class TwoPacketsThenIdleStream(byte[] p1, byte[] p2) : IHidReportStream
    {
        private int _step;
        public int MaxInputReportLength => Math.Max(p1.Length, p2.Length);
        public HidReadResult Read(byte[] buffer, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return new HidReadResult(HidReadResultKind.Disconnected, 0);
            if (_step == 0)
            {
                _step = 1;
                Array.Copy(p1, buffer, p1.Length);
                return new HidReadResult(HidReadResultKind.Data, p1.Length);
            }
            if (_step == 1)
            {
                _step = 2;
                Array.Copy(p2, buffer, p2.Length);
                return new HidReadResult(HidReadResultKind.Data, p2.Length);
            }
            try { Task.Delay(30, ct).GetAwaiter().GetResult(); } catch { }
            return ct.IsCancellationRequested
                ? new HidReadResult(HidReadResultKind.Disconnected, 0)
                : new HidReadResult(HidReadResultKind.Timeout, 0);
        }
        public void Dispose() { }
    }
}
