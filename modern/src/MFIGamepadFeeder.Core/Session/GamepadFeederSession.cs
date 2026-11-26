using System.Diagnostics;
using MFIGamepadFeeder.Core.Backends;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Session;

public enum FeederConnectionStatus
{
    Stopped,
    Connecting,
    Connected,
    Reconnecting,
    Faulted,
}

public sealed record FeederTelemetry(
    FeederConnectionStatus Status,
    string BackendName,
    GamepadState LastState,
    byte[] LastRawBytes,
    double ReportsPerSecond,
    long SubmittedReports,
    long SkippedReports,
    int ReconnectCount);

/// <summary>
/// Manages one active MFi-HID -> Virtual Xbox 360 feeding loop.
/// Features:
/// - Thread-safe Start/Stop/Dispose lifecycle with CancellationToken shutdown (no Thread.Abort()).
/// - Single atomic report submission per changed state (no redundant kernel IOCTLs).
/// - Immediate neutral reset + automatic Bluetooth reconnect on sleep/disconnect.
/// - Truly immediate live runtime option updates (deadzone, button threshold, Y-axis inversion),
///   even while the Bluetooth controller is idle between reports.
/// - Always-fresh telemetry raw bytes (fast tap/release never leaves UI bytes stale).
/// </summary>
public sealed class GamepadFeederSession : IDisposable
{
    private readonly IHidReportSource _hidSource;
    private readonly HidReportMapper _mapper;
    private readonly IVirtualController _controller;
    private readonly int _reconnectDelayMs;

    private readonly object _lifecycleLock = new();
    private readonly object _optionsLock = new();
    private MappingRuntimeOptions _runtimeOptions;
    private volatile bool _optionsDirty;

    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private IHidReportStream? _activeStream;
    private bool _disposed;

    public event Action<string>? LogMessage;
    public event Action<FeederTelemetry>? TelemetryUpdated;

    public GamepadFeederSession(
        IHidReportSource hidSource,
        HidReportMapper mapper,
        IVirtualController controller,
        MappingRuntimeOptions? initialOptions = null,
        int reconnectDelayMs = 500)
    {
        _hidSource = hidSource ?? throw new ArgumentNullException(nameof(hidSource));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _runtimeOptions = initialOptions ?? new MappingRuntimeOptions();
        _reconnectDelayMs = Math.Clamp(reconnectDelayMs, 10, 5000);
    }

    public bool IsRunning
    {
        get
        {
            lock (_lifecycleLock)
            {
                return _workerTask is { IsCompleted: false };
            }
        }
    }

    public void UpdateRuntimeOptions(MappingRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_optionsLock)
        {
            _runtimeOptions = options;
            _optionsDirty = true;
        }
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_workerTask is { IsCompleted: false })
            {
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _workerTask = Task.Run(() => RunLoop(token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? workerTask;

        lock (_lifecycleLock)
        {
            cts = _cts;
            workerTask = _workerTask;
            _cts = null;
            _workerTask = null;
        }

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch
        {
            // Ignore double-cancel.
        }

        // Closing the active HID stream wakes any blocking Read() immediately.
        Interlocked.Exchange(ref _activeStream, null)?.Dispose();

        try
        {
            workerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Worker observed cancellation.
        }

        try
        {
            _controller.Reset();
        }
        catch
        {
            // Best-effort neutral reset on stop.
        }

        cts.Dispose();
    }

    private void RunLoop(CancellationToken ct)
    {
        var reconnectCount = 0;
        long submittedReports = 0;
        long skippedReports = 0;
        var hasConnectedOnce = false;
        var lastState = GamepadState.Neutral;
        var lastRawBuffer = Array.Empty<byte>();
        var lastRawLength = 0;
        string? lastDiagnostic = null;

        EmitTelemetry(FeederConnectionStatus.Connecting, lastState, Array.Empty<byte>(), 0, submittedReports, skippedReports, reconnectCount);

        while (!ct.IsCancellationRequested)
        {
            if (!_hidSource.TryOpen(out var stream, out var openError) || stream is null)
            {
                var status = hasConnectedOnce ? FeederConnectionStatus.Reconnecting : FeederConnectionStatus.Connecting;
                EmitTelemetry(status, GamepadState.Neutral, SnapshotRaw(lastRawBuffer, lastRawLength), 0, submittedReports, skippedReports, reconnectCount);

                if (!string.IsNullOrWhiteSpace(openError) && openError != lastDiagnostic)
                {
                    lastDiagnostic = openError;
                    Log(openError);
                }

                try
                {
                    Task.Delay(_reconnectDelayMs, ct).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            lastDiagnostic = null;
            if (hasConnectedOnce)
            {
                reconnectCount++;
                Log($"Reconnected to {_hidSource.DisplayName} (attempt #{reconnectCount}).");
            }
            else
            {
                hasConnectedOnce = true;
                Log($"Connected to {_hidSource.DisplayName} -> {_controller.BackendName} slot {_controller.SlotIndex}.");
            }

            Interlocked.Exchange(ref _activeStream, stream);
            var controllerFaulted = false;

            try
            {
                using (stream)
                {
                    var buffer = new byte[Math.Max(64, stream.MaxInputReportLength)];
                    if (lastRawBuffer.Length < buffer.Length)
                    {
                        lastRawBuffer = new byte[buffer.Length];
                    }

                    var hasSubmittedAny = false;
                    var telemetryDirty = false;
                    var rateWatch = Stopwatch.StartNew();
                    var telemetryWatch = Stopwatch.StartNew();
                    var reportsInWindow = 0;
                    var currentRps = 0.0;

                    EmitTelemetry(FeederConnectionStatus.Connected, lastState, SnapshotRaw(lastRawBuffer, lastRawLength), 0, submittedReports, skippedReports, reconnectCount);

                    while (!ct.IsCancellationRequested)
                    {
                        var read = stream.Read(buffer, ct);
                        if (read.Kind == HidReadResultKind.Timeout)
                        {
                            // If user changed deadzone/threshold/invert while the controller is idle,
                            // immediately re-evaluate the last received raw report and submit the new state.
                            if (_optionsDirty && lastRawLength > 0)
                            {
                                var opts = ReadRuntimeOptions(clearDirty: true);
                                if (_mapper.TryMapReport(lastRawBuffer.AsSpan(0, lastRawLength), out var remapped, out _, opts))
                                {
                                    if (!hasSubmittedAny || remapped != lastState)
                                    {
                                        _controller.SubmitState(remapped);
                                        lastState = remapped;
                                        hasSubmittedAny = true;
                                        submittedReports++;
                                        telemetryDirty = true;
                                    }
                                }
                            }

                            if ((telemetryDirty && telemetryWatch.ElapsedMilliseconds >= 16) || telemetryWatch.ElapsedMilliseconds >= 250)
                            {
                                if (rateWatch.ElapsedMilliseconds >= 250)
                                {
                                    currentRps = reportsInWindow / Math.Max(0.001, rateWatch.Elapsed.TotalSeconds);
                                    reportsInWindow = 0;
                                    rateWatch.Restart();
                                }

                                telemetryDirty = false;
                                telemetryWatch.Restart();
                                EmitTelemetry(
                                    FeederConnectionStatus.Connected,
                                    lastState,
                                    SnapshotRaw(lastRawBuffer, lastRawLength),
                                    currentRps,
                                    submittedReports,
                                    skippedReports,
                                    reconnectCount);
                            }

                            continue;
                        }

                        if (read.Kind == HidReadResultKind.Disconnected)
                        {
                            if (!ct.IsCancellationRequested)
                            {
                                Log($"Controller stream disconnected ({read.ErrorMessage ?? "device closed"}). Resetting virtual controller to neutral and waiting to reconnect...");
                            }
                            break;
                        }

                        reportsInWindow++;
                        var slice = buffer.AsSpan(0, read.BytesRead);
                        var currentOpts = ReadRuntimeOptions(clearDirty: true);

                        if (_mapper.TryMapReport(slice, out var mappedState, out var diagnostic, currentOpts))
                        {
                            // Always retain the latest raw report bytes so fast tap/release never leaves telemetry stale.
                            if (lastRawBuffer.Length < slice.Length)
                            {
                                lastRawBuffer = new byte[slice.Length];
                            }

                            slice.CopyTo(lastRawBuffer);
                            lastRawLength = slice.Length;

                            var stateChanged = !hasSubmittedAny || mappedState != lastState;
                            if (stateChanged)
                            {
                                _controller.SubmitState(mappedState);
                                lastState = mappedState;
                                hasSubmittedAny = true;
                                submittedReports++;
                                telemetryDirty = true;
                            }

                            var shouldEmit =
                                !hasSubmittedAny ||
                                (telemetryDirty && telemetryWatch.ElapsedMilliseconds >= 16) ||
                                telemetryWatch.ElapsedMilliseconds >= 50 ||
                                submittedReports == 1;

                            if (shouldEmit)
                            {
                                if (rateWatch.ElapsedMilliseconds >= 500)
                                {
                                    currentRps = reportsInWindow / Math.Max(0.001, rateWatch.Elapsed.TotalSeconds);
                                    reportsInWindow = 0;
                                    rateWatch.Restart();
                                }

                                telemetryDirty = false;
                                telemetryWatch.Restart();
                                EmitTelemetry(
                                    FeederConnectionStatus.Connected,
                                    lastState,
                                    SnapshotRaw(lastRawBuffer, lastRawLength),
                                    currentRps,
                                    submittedReports,
                                    skippedReports,
                                    reconnectCount);
                            }
                        }
                        else
                        {
                            skippedReports++;
                            if (!string.IsNullOrWhiteSpace(diagnostic) && diagnostic != lastDiagnostic)
                            {
                                lastDiagnostic = diagnostic;
                                Log(diagnostic);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Log($"Feeder loop error: {ex.Message}");
                controllerFaulted = true;
            }
            finally
            {
                Interlocked.CompareExchange(ref _activeStream, null, stream);
            }

            // Immediately neutralize virtual controller on disconnect so games never get stuck sticks/buttons.
            var resetFailed = false;
            try
            {
                _controller.Reset();
                lastState = GamepadState.Neutral;
                lastRawLength = 0;
            }
            catch (Exception ex)
            {
                resetFailed = true;
                Log($"Failed to reset virtual controller after disconnect: {ex.Message}");
            }

            if (controllerFaulted && resetFailed && !ct.IsCancellationRequested)
            {
                Log($"Virtual controller backend '{_controller.BackendName}' stopped responding. Stopping feeder session.");
                EmitTelemetry(FeederConnectionStatus.Faulted, GamepadState.Neutral, Array.Empty<byte>(), 0, submittedReports, skippedReports, reconnectCount);
                return;
            }

            if (!ct.IsCancellationRequested)
            {
                EmitTelemetry(FeederConnectionStatus.Reconnecting, GamepadState.Neutral, Array.Empty<byte>(), 0, submittedReports, skippedReports, reconnectCount);
                try
                {
                    Task.Delay(_reconnectDelayMs, ct).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        EmitTelemetry(FeederConnectionStatus.Stopped, GamepadState.Neutral, Array.Empty<byte>(), 0, submittedReports, skippedReports, reconnectCount);
    }

    private MappingRuntimeOptions ReadRuntimeOptions(bool clearDirty)
    {
        lock (_optionsLock)
        {
            if (clearDirty)
            {
                _optionsDirty = false;
            }

            return _runtimeOptions;
        }
    }

    private static byte[] SnapshotRaw(byte[] buffer, int length)
    {
        if (length <= 0 || buffer.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var copy = new byte[length];
        Buffer.BlockCopy(buffer, 0, copy, 0, length);
        return copy;
    }

    private void Log(string message) => LogMessage?.Invoke(message);

    private void EmitTelemetry(
        FeederConnectionStatus status,
        GamepadState state,
        byte[] raw,
        double rps,
        long submitted,
        long skipped,
        int reconnects)
    {
        TelemetryUpdated?.Invoke(new FeederTelemetry(
            status,
            _controller.BackendName,
            state,
            raw,
            rps,
            submitted,
            skipped,
            reconnects));
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
        _controller.Dispose();
    }
}
