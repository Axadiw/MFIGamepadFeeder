using System.Diagnostics;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Diagnostics;

public enum HidTestStatus
{
    Stopped,
    Connecting,
    Waiting,
    Receiving,
    Disconnected,
    Faulted,
}

public sealed record HidTestButtonEvent(DateTimeOffset Timestamp, XInputButtons Button, bool IsPressed);

public sealed record HidTestSnapshot(
    HidTestStatus Status,
    string Message,
    GamepadState State,
    long ReportCount,
    long ReceivedReportCount,
    long ButtonPressCount,
    long ButtonReleaseCount,
    DateTimeOffset? LastReportAtUtc,
    double ReportsPerSecond,
    double? AverageReportIntervalMs,
    double? MaxReportIntervalMs,
    byte[] RawBytes,
    IReadOnlyList<HidTestButtonEvent> RecentEvents);

/// <summary>Reads and decodes physical HID input without creating or writing to a virtual controller.</summary>
public sealed class HidTestSession : IAsyncDisposable
{
    private static readonly MappingRuntimeOptions RawOptions = new(Deadzone: 0, ButtonThreshold: 1);
    private readonly IHidReportSource _source;
    private readonly HidReportMapper _mapper;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Queue<HidTestButtonEvent> _events = new();
    private IHidReportStream? _activeStream;
    private Task? _worker;
    private Task? _stopTask;
    private bool _disposed;
    private long _stateVersion;
    private long? _stateReceivedTimestamp;
    private HidTestSnapshot _snapshot = new(
        HidTestStatus.Stopped, "Stopped.", GamepadState.Neutral,
        0, 0, 0, 0, null, 0, null, null, [], []);

    public HidTestSession(IHidReportSource source, HidReportMapper mapper)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_worker is not null || _stopTask is not null)
            {
                throw new InvalidOperationException("Create a new HID test session to start again.");
            }

            _snapshot = _snapshot with { Status = HidTestStatus.Connecting, Message = $"Opening {_source.DisplayName}..." };
            _worker = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public HidTestSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return _snapshot with { RawBytes = (byte[])_snapshot.RawBytes.Clone(), RecentEvents = _snapshot.RecentEvents.ToArray() };
        }
    }

    public (GamepadState State, long Version, long? ReceivedTimestamp) GetVisualState()
    {
        lock (_sync)
        {
            return (_snapshot.State, _stateVersion, _stateReceivedTimestamp);
        }
    }

    public Task StopAsync()
    {
        lock (_sync)
        {
            return _stopTask ??= Task.Run(StopCoreAsync);
        }
    }

    private async Task StopCoreAsync()
    {
        _cts.Cancel();
        IHidReportStream? stream;
        Task? worker;
        lock (_sync)
        {
            stream = _activeStream;
            worker = _worker;
        }

        // Closing the stream wakes a pending blocking HID read.
        try { stream?.Dispose(); } catch { }
        if (worker is not null)
        {
            await worker.ConfigureAwait(false);
        }

        SetInactive(HidTestStatus.Stopped, "Stopped.");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!_source.TryOpen(out var stream, out var error) || stream is null)
                {
                    SetInactive(HidTestStatus.Disconnected, error ?? "Controller unavailable. Retrying...");
                }
                else
                {
                    using (stream)
                    {
                        lock (_sync)
                        {
                            _activeStream = stream;
                        }

                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        ReadConnected(stream, ct);
                    }

                    lock (_sync)
                    {
                        _activeStream = null;
                    }
                }

                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SetInactive(HidTestStatus.Faulted, $"Input test failed: {ex.Message}");
        }
        finally
        {
            lock (_sync)
            {
                _activeStream = null;
            }
        }
    }

    private void ReadConnected(IHidReportStream stream, CancellationToken ct)
    {
        var buffer = new byte[Math.Max(64, stream.MaxInputReportLength)];
        var intervals = new Queue<(long At, double Ms)>();
        long? previousArrival = null;
        lock (_sync)
        {
            _snapshot = _snapshot with { Status = HidTestStatus.Waiting, Message = "Connected. Move a stick or press a button." };
        }

        while (!ct.IsCancellationRequested)
        {
            var read = stream.Read(buffer, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (read.Kind == HidReadResultKind.Disconnected)
            {
                SetInactive(HidTestStatus.Disconnected, read.ErrorMessage ?? "Controller disconnected. Retrying...");
                return;
            }

            if (read.Kind == HidReadResultKind.Timeout)
            {
                // Idle/timeout gaps are not evidence of input latency or an active report interval.
                previousArrival = null;
                intervals.Clear();
                lock (_sync)
                {
                    _snapshot = _snapshot with
                    {
                        Status = HidTestStatus.Waiting,
                        Message = "Waiting for input. The controller may report only when controls change.",
                        ReportsPerSecond = 0,
                        AverageReportIntervalMs = null,
                        MaxReportIntervalMs = null,
                    };
                }
                continue;
            }

            if (read.BytesRead <= 0 || read.BytesRead > buffer.Length)
            {
                SetIgnoredReport("Ignored an invalid HID report length.");
                continue;
            }

            var report = buffer.AsSpan(0, read.BytesRead);
            var arrival = Stopwatch.GetTimestamp();
            var now = DateTimeOffset.UtcNow;
            if (previousArrival.HasValue)
            {
                intervals.Enqueue((arrival, Stopwatch.GetElapsedTime(previousArrival.Value, arrival).TotalMilliseconds));
            }
            previousArrival = arrival;
            while (intervals.TryPeek(out var oldest) && Stopwatch.GetElapsedTime(oldest.At, arrival).TotalSeconds > 1)
            {
                intervals.Dequeue();
            }

            var average = intervals.Count > 0 ? intervals.Average(interval => interval.Ms) : (double?)null;
            var max = intervals.Count > 0 ? intervals.Max(interval => interval.Ms) : (double?)null;
            lock (_sync)
            {
                // Capture every received packet, including reports the selected mapping cannot decode.
                _snapshot = _snapshot with
                {
                    Status = HidTestStatus.Receiving,
                    Message = "Receiving physical controller input.",
                    ReceivedReportCount = _snapshot.ReceivedReportCount + 1,
                    LastReportAtUtc = now,
                    ReportsPerSecond = average is > 0 ? 1000.0 / average.Value : 0,
                    AverageReportIntervalMs = average,
                    MaxReportIntervalMs = max,
                    RawBytes = report.ToArray(),
                };
            }

            if (!_mapper.TryMapReport(report, out var state, out var diagnostic, RawOptions))
            {
                SetIgnoredReport(diagnostic ?? $"Received HID report ID 0x{report[0]:X2}; ignored by the selected mapping's report ID filter.");
                continue;
            }

            lock (_sync)
            {
                if (state != _snapshot.State)
                {
                    _stateVersion++;
                    _stateReceivedTimestamp = arrival;
                }

                var changed = _snapshot.State.Buttons ^ state.Buttons;
                var presses = _snapshot.ButtonPressCount;
                var releases = _snapshot.ButtonReleaseCount;
                for (var bit = 0; bit < 16; bit++)
                {
                    var button = (XInputButtons)(1 << bit);
                    if ((changed & button) == 0)
                    {
                        continue;
                    }

                    var pressed = (state.Buttons & button) != 0;
                    if (pressed) { presses++; } else { releases++; }
                    _events.Enqueue(new HidTestButtonEvent(now, button, pressed));
                    if (_events.Count > 20) { _events.Dequeue(); }
                }

                _snapshot = _snapshot with
                {
                    State = state,
                    ReportCount = _snapshot.ReportCount + 1,
                    ButtonPressCount = presses,
                    ButtonReleaseCount = releases,
                    RecentEvents = _events.ToArray(),
                };
            }
        }
    }

    private void SetIgnoredReport(string message)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with { Message = message };
        }
    }

    private void SetInactive(HidTestStatus status, string message)
    {
        lock (_sync)
        {
            if (_snapshot.State != GamepadState.Neutral)
            {
                _stateVersion++;
            }
            _stateReceivedTimestamp = null;

            _snapshot = _snapshot with
            {
                Status = status,
                Message = message,
                State = GamepadState.Neutral,
                RawBytes = [],
                ReportsPerSecond = 0,
                AverageReportIntervalMs = null,
                MaxReportIntervalMs = null,
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        lock (_sync)
        {
            if (!_disposed)
            {
                _disposed = true;
                _cts.Dispose();
            }
        }
    }
}
