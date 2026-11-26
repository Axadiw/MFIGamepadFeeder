using System.Collections.Concurrent;
using MFIGamepadFeeder.Core.Backends;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;
using MFIGamepadFeeder.Core.Session;
using Xunit;

namespace MFIGamepadFeeder.Tests;

public sealed class GamepadFeederSessionTests
{
    private static HidReportMapper CreateSimpleNimbusMapper()
    {
        var mapping = new GamepadMapping
        {
            MappingItems =
            [
                new GamepadMappingItem { Type = GamepadMappingItemType.Empty },
                new GamepadMappingItem { Type = GamepadMappingItemType.Button, ButtonType = XInputButtons.A },
                new GamepadMappingItem { Type = GamepadMappingItemType.Axis, AxisType = AxisType.Ly, ConvertAxis = true },
            ],
        };
        return new HidReportMapper(mapping);
    }

    [Fact]
    public async Task Session_DeduplicatesIdenticalStates_SkipsShortReports_AndRecoversFromDisconnect()
    {
        var mapper = CreateSimpleNimbusMapper();
        var controller = new FakeVirtualController();

        // Stream 1: sends A pressed (x3 identical -> 1 submit!), 1 short bad report (skipped), then disconnects!
        var stream1 = new FakeHidStream(
        [
            new byte[] { 0, 255, 100 },
            new byte[] { 0, 255, 100 }, // duplicate -> suppressed
            new byte[] { 0, 255, 100 }, // duplicate -> suppressed
            new byte[] { 0 },           // short -> skipped safely
            null,                       // disconnect!
        ]);

        // Stream 2 (after Bluetooth reconnect): sends A released + LY full negative, then waits for cancel.
        var stream2 = new FakeHidStream(
        [
            new byte[] { 0, 0, 128 },
        ], blockAfterQueueEnds: true);

        var source = new FakeHidSource(
        [
            stream1,
            null, // transient unavailable while Bluetooth wakes
            stream2,
        ]);

        var reconnectObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new ConcurrentQueue<string>();

        using var session = new GamepadFeederSession(
            source,
            mapper,
            controller,
            new MappingRuntimeOptions(Deadzone: 0.0),
            reconnectDelayMs: 25);

        session.LogMessage += msg => logs.Enqueue(msg);
        session.TelemetryUpdated += t =>
        {
            if (t.ReconnectCount >= 1 && t.LastState.LeftThumbY == short.MinValue)
            {
                reconnectObserved.TrySetResult(true);
            }
        };

        session.Start();

        var completed = await Task.WhenAny(reconnectObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(reconnectObserved.Task, completed);

        session.Stop();

        // Verify:
        // 1) Duplicate consecutive A-pressed reports caused only 1 submission before disconnect.
        // 2) Disconnect immediately reset controller to neutral (no stuck A button!).
        // 3) Reconnect resumed and submitted LY = short.MinValue.
        // 4) Stop reset controller back to neutral cleanly.
        var submitted = controller.SubmittedStates.ToArray();
        Assert.Contains(submitted, s => s.Buttons == XInputButtons.A);
        Assert.Equal(1, submitted.Count(s => s.Buttons == XInputButtons.A));
        Assert.True(controller.ResetCount >= 2, $"Expected neutral resets on disconnect and stop, got {controller.ResetCount}");
        Assert.Contains(submitted, s => s.LeftThumbY == short.MinValue && s.Buttons == XInputButtons.None);
        Assert.Equal(GamepadState.Neutral, controller.LastState);
    }

    [Fact]
    public async Task Session_AppliesLiveRuntimeInvertOptionWithoutRestarting()
    {
        var mapper = CreateSimpleNimbusMapper();
        var controller = new FakeVirtualController();

        var gate = new SemaphoreSlim(0, 1);
        var stream = new GatedHidStream(
            firstPacket: [0, 0, 127],  // LY = +127
            secondPacket: [0, 0, 127], // same raw byte, after InvertLeftY toggled ON!
            secondPacketGate: gate);

        var source = new FakeHidSource([stream]);
        var firstSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invertedSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        controller.OnSubmit += state =>
        {
            if (state.LeftThumbY == short.MaxValue) firstSeen.TrySetResult(true);
            if (state.LeftThumbY == short.MinValue) invertedSeen.TrySetResult(true);
        };

        using var session = new GamepadFeederSession(
            source,
            mapper,
            controller,
            new MappingRuntimeOptions(Deadzone: 0.0, InvertLeftY: false),
            reconnectDelayMs: 25);

        session.Start();
        Assert.Same(firstSeen.Task, await Task.WhenAny(firstSeen.Task, Task.Delay(3000)));

        // Flip InvertLeftY live while running!
        session.UpdateRuntimeOptions(new MappingRuntimeOptions(Deadzone: 0.0, InvertLeftY: true));
        gate.Release();

        Assert.Same(invertedSeen.Task, await Task.WhenAny(invertedSeen.Task, Task.Delay(3000)));
        session.Stop();
    }

    private sealed class FakeVirtualController : IVirtualController
    {
        public string BackendName => "FakeBackend";
        public uint SlotIndex => 1;
        public ConcurrentQueue<GamepadState> SubmittedStates { get; } = new();
        public GamepadState LastState { get; private set; } = GamepadState.Neutral;
        public int ResetCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public event Action<GamepadState>? OnSubmit;

        public void SubmitState(in GamepadState state)
        {
            SubmittedStates.Enqueue(state);
            LastState = state;
            OnSubmit?.Invoke(state);
        }

        public void Reset()
        {
            ResetCount++;
            LastState = GamepadState.Neutral;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class FakeHidSource : IHidReportSource
    {
        private readonly Queue<IHidReportStream?> _streams;

        public FakeHidSource(IEnumerable<IHidReportStream?> streams)
        {
            _streams = new Queue<IHidReportStream?>(streams);
        }

        public string DisplayName => "Fake SteelSeries Nimbus";

        public bool TryOpen(out IHidReportStream? stream, out string? errorMessage)
        {
            if (_streams.Count == 0)
            {
                stream = null;
                errorMessage = "Disconnected";
                return false;
            }

            stream = _streams.Dequeue();
            errorMessage = stream is null ? "Temporarily offline" : null;
            return stream is not null;
        }
    }

    private sealed class FakeHidStream : IHidReportStream
    {
        private readonly Queue<byte[]?> _packets;
        private readonly bool _blockAfterQueueEnds;

        public FakeHidStream(IEnumerable<byte[]?> packets, bool blockAfterQueueEnds = false)
        {
            _packets = new Queue<byte[]?>(packets);
            _blockAfterQueueEnds = blockAfterQueueEnds;
        }

        public int MaxInputReportLength => 18;

        public HidReadResult Read(byte[] buffer, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0);
            }

            if (_packets.Count == 0)
            {
                if (_blockAfterQueueEnds)
                {
                    try { Task.Delay(50, cancellationToken).GetAwaiter().GetResult(); } catch { }
                    return cancellationToken.IsCancellationRequested
                        ? new HidReadResult(HidReadResultKind.Disconnected, 0)
                        : new HidReadResult(HidReadResultKind.Timeout, 0);
                }

                return new HidReadResult(HidReadResultKind.Disconnected, 0, "EOF");
            }

            var next = _packets.Dequeue();
            if (next is null)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0, "Simulated BT sleep");
            }

            Array.Copy(next, buffer, next.Length);
            return new HidReadResult(HidReadResultKind.Data, next.Length);
        }

        public void Dispose() { }
    }

    private sealed class GatedHidStream : IHidReportStream
    {
        private readonly byte[] _firstPacket;
        private readonly byte[] _secondPacket;
        private readonly SemaphoreSlim _gate;
        private int _step;

        public GatedHidStream(byte[] firstPacket, byte[] secondPacket, SemaphoreSlim secondPacketGate)
        {
            _firstPacket = firstPacket;
            _secondPacket = secondPacket;
            _gate = secondPacketGate;
        }

        public int MaxInputReportLength => 18;

        public HidReadResult Read(byte[] buffer, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new HidReadResult(HidReadResultKind.Disconnected, 0);
            }

            if (_step == 0)
            {
                _step = 1;
                Array.Copy(_firstPacket, buffer, _firstPacket.Length);
                return new HidReadResult(HidReadResultKind.Data, _firstPacket.Length);
            }

            if (_step == 1)
            {
                if (!_gate.Wait(50, cancellationToken))
                {
                    return new HidReadResult(HidReadResultKind.Timeout, 0);
                }

                _step = 2;
                Array.Copy(_secondPacket, buffer, _secondPacket.Length);
                return new HidReadResult(HidReadResultKind.Data, _secondPacket.Length);
            }

            try { Task.Delay(50, cancellationToken).GetAwaiter().GetResult(); } catch { }
            return new HidReadResult(HidReadResultKind.Timeout, 0);
        }

        public void Dispose() { }
    }
}
