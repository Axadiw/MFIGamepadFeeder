using System.Collections.Concurrent;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Diagnostics;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;
using Xunit;

namespace MFIGamepadFeeder.Tests;

public sealed class HidTestSessionTests
{
    private static HidReportMapper CreateMapper(byte? reportId = null) => new(new GamepadMapping
    {
        ReportId = reportId,
        MappingItems =
        [
            new GamepadMappingItem { Type = GamepadMappingItemType.Empty },
            new GamepadMappingItem { Type = GamepadMappingItemType.Button, ButtonType = XInputButtons.A },
        ],
    });

    [Fact]
    public async Task QuickTap_IsRetainedBeforeTheFirstUiPoll_AndInvalidReportsAreIgnored()
    {
        using var stream = new TestStream();
        stream.Add([0, 255]);
        stream.Add([0]); // Too short for the mapping.
        stream.Add([0, 0]);
        await using var session = new HidTestSession(new TestSource(stream), CreateMapper());
        session.Start();
        await stream.WaitForReadsAsync(3);

        // The worker has now returned to Read, so both decoded edges precede this UI snapshot.
        var snapshot = session.GetSnapshot();
        Assert.Equal(2, snapshot.ReportCount);
        Assert.Equal(3, snapshot.ReceivedReportCount);
        Assert.Equal(1, snapshot.ButtonPressCount);
        Assert.Equal(1, snapshot.ButtonReleaseCount);
        Assert.Equal(GamepadState.Neutral, snapshot.State);
        Assert.Collection(snapshot.RecentEvents,
            press => { Assert.Equal(XInputButtons.A, press.Button); Assert.True(press.IsPressed); },
            release => { Assert.Equal(XInputButtons.A, release.Button); Assert.False(release.IsPressed); });

        // Callers cannot mutate the session's stored report through a snapshot.
        snapshot.RawBytes[1] = 255;
        Assert.Equal(0, session.GetSnapshot().RawBytes[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnmappedReport_RemainsVisibleWithoutChangingDecodedStateOrButtonCounts(bool filteredReportId)
    {
        using var stream = new TestStream();
        stream.Add([0, 255]);
        byte[] rejected = filteredReportId ? [7, 0] : [7];
        stream.Add(rejected);
        await using var session = new HidTestSession(new TestSource(stream), CreateMapper(filteredReportId ? (byte)0 : null));
        session.Start();
        await stream.WaitForReadsAsync(2);

        var snapshot = session.GetSnapshot();
        Assert.Equal(HidTestStatus.Receiving, snapshot.Status);
        Assert.Equal(2, snapshot.ReceivedReportCount);
        Assert.Equal(1, snapshot.ReportCount);
        Assert.Equal(rejected, snapshot.RawBytes);
        Assert.NotNull(snapshot.LastReportAtUtc);
        Assert.Contains(filteredReportId ? "report ID filter" : "short HID report", snapshot.Message);
        Assert.Equal(XInputButtons.A, snapshot.State.Buttons);
        Assert.Equal(1, snapshot.ButtonPressCount);
        Assert.Equal(0, snapshot.ButtonReleaseCount);
        Assert.Single(snapshot.RecentEvents);
    }

    [Fact]
    public async Task VisualState_ChangesOnlyForDecodedChanges_AndKeepsTheOriginalReceiptTimestamp()
    {
        using var stream = new TestStream();
        await using var session = new HidTestSession(new TestSource(stream), CreateMapper(reportId: 0));
        Assert.Equal((GamepadState.Neutral, 0L, (long?)null), session.GetVisualState());
        stream.Add([0, 255]);
        session.Start();
        await stream.WaitForReadsAsync(1);
        var pressed = session.GetVisualState();
        Assert.Equal(XInputButtons.A, pressed.State.Buttons);
        Assert.Equal(1, pressed.Version);
        Assert.NotNull(pressed.ReceivedTimestamp);

        stream.Add([0, 255]); // An identical decoded report must not make the state look newer.
        stream.Add([0]);      // Short report.
        stream.Add([7, 0]);   // Filtered report ID.
        await stream.WaitForReadsAsync(4);
        Assert.Equal(pressed, session.GetVisualState());
        Assert.Equal(4, session.GetSnapshot().ReceivedReportCount);
        Assert.Equal(2, session.GetSnapshot().ReportCount);

        stream.Add([0, 0]);
        await stream.WaitForReadsAsync(5);
        var released = session.GetVisualState();
        Assert.Equal(GamepadState.Neutral, released.State);
        Assert.Equal(2, released.Version);
        Assert.True(released.ReceivedTimestamp >= pressed.ReceivedTimestamp);
    }

    [Fact]
    public async Task Disconnect_ClearsHeldStateWithoutInventingARelease_AndStopCancelsRetry()
    {
        using var stream = new TestStream();
        stream.Add([0, 255]);
        stream.Disconnect();
        await using var session = new HidTestSession(new TestSource(stream), CreateMapper());
        session.Start();
        await WaitUntilAsync(() => session.GetSnapshot().Status == HidTestStatus.Disconnected);

        var disconnected = session.GetSnapshot();
        Assert.Equal(GamepadState.Neutral, disconnected.State);
        Assert.Empty(disconnected.RawBytes);
        Assert.Equal(1, disconnected.ButtonPressCount);
        Assert.Equal(0, disconnected.ButtonReleaseCount);
        Assert.Null(disconnected.AverageReportIntervalMs);
        Assert.Equal((GamepadState.Neutral, 2L, (long?)null), session.GetVisualState());
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(HidTestStatus.Stopped, session.GetSnapshot().Status);
    }

    [Fact]
    public async Task Timeout_BreaksCadenceWindow_AndStopWakesAnIdleRead()
    {
        using var stream = new TestStream();
        stream.Add([0, 255]);
        stream.Add([0, 0]);
        stream.Timeout();
        await using var session = new HidTestSession(new TestSource(stream), CreateMapper());
        session.Start();
        await stream.WaitForReadsAsync(3);

        var waiting = session.GetSnapshot();
        Assert.Equal(HidTestStatus.Waiting, waiting.Status);
        Assert.Equal(0, waiting.ReportsPerSecond);
        Assert.Null(waiting.AverageReportIntervalMs);
        Assert.Null(waiting.MaxReportIntervalMs);
        Assert.Equal(2, waiting.ReportCount);
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(stream.IsDisposed);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class TestSource(TestStream stream) : IHidReportSource
    {
        public string DisplayName => "Test physical HID";
        public bool TryOpen(out IHidReportStream? opened, out string? errorMessage)
        {
            opened = stream;
            errorMessage = null;
            return true;
        }
    }

    private sealed class TestStream : IHidReportStream
    {
        private readonly BlockingCollection<(HidReadResultKind Kind, byte[] Bytes)> _reads = new();
        private int _returnedReads;
        private int _enteredReads;
        public int MaxInputReportLength => 2;
        public bool IsDisposed { get; private set; }
        public void Add(byte[] report) => _reads.Add((HidReadResultKind.Data, report));
        public void Disconnect() => _reads.Add((HidReadResultKind.Disconnected, []));
        public void Timeout() => _reads.Add((HidReadResultKind.Timeout, []));

        public HidReadResult Read(byte[] buffer, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _enteredReads);
            try
            {
                var next = _reads.Take(cancellationToken);
                next.Bytes.CopyTo(buffer, 0);
                Interlocked.Increment(ref _returnedReads);
                return new HidReadResult(next.Kind, next.Bytes.Length);
            }
            catch (InvalidOperationException)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0);
            }
        }

        public Task WaitForReadsAsync(int count) => WaitUntilAsync(
            () => Volatile.Read(ref _returnedReads) >= count && Volatile.Read(ref _enteredReads) > count);

        public void Dispose()
        {
            IsDisposed = true;
            _reads.CompleteAdding();
        }
    }
}
