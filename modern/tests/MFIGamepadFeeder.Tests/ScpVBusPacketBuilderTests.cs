using System.Buffers.Binary;
using MFIGamepadFeeder.Core.Backends;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace MFIGamepadFeeder.Tests;

public sealed class ScpVBusPacketBuilderTests
{
    private readonly ITestOutputHelper _output;

    public ScpVBusPacketBuilderTests(ITestOutputHelper output)
    {
        _output = output;
    }
    [Fact]
    public void IoctlConstants_MatchVerifiedScpVBus1712Contract()
    {
        Assert.Equal(0x002AA004u, ScpVBusPacketBuilder.IoctlPlugIn);
        Assert.Equal(0x002AA008u, ScpVBusPacketBuilder.IoctlUnPlug);
        Assert.Equal(0x002AE010u, ScpVBusPacketBuilder.IoctlReport);
        Assert.Equal(0x002AE404u, ScpVBusPacketBuilder.IoctlIsDevPlugged);
        Assert.Equal(0x002AE410u, ScpVBusPacketBuilder.IoctlVersion);
        Assert.Equal(new Guid("F679F562-3164-42CE-A4DB-E7DDBE723909"), ScpVBusPacketBuilder.DeviceInterfaceGuid);
    }

    [Fact]
    public void BuildPlugInAndUnPlugPackets_EncodeExactHeaderAndFlags()
    {
        var plug = new byte[16];
        ScpVBusPacketBuilder.BuildPlugInPacket(2, plug);
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(plug.AsSpan(0, 4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(plug.AsSpan(4, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(plug.AsSpan(8, 4)));

        var forceUnplug = new byte[16];
        ScpVBusPacketBuilder.BuildUnPlugPacket(3, force: true, forceUnplug);
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(forceUnplug.AsSpan(0, 4)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(forceUnplug.AsSpan(4, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(forceUnplug.AsSpan(8, 4)));
    }

    [Fact]
    public void BuildReportPacket_EncodesFullAtomicXusbReportIncludingDpadAndExtremes()
    {
        var state = new GamepadState(
            Buttons: XInputButtons.DpadUp | XInputButtons.DpadLeft | XInputButtons.A | XInputButtons.Start,
            LeftTrigger: 0xAA,
            RightTrigger: 0x55,
            LeftThumbX: short.MaxValue,
            LeftThumbY: short.MinValue,
            RightThumbX: -1234,
            RightThumbY: 4321);

        var buf = new byte[ScpVBusPacketBuilder.ReportPacketSize];
        ScpVBusPacketBuilder.BuildReportPacket(1, state, buf);

        Assert.Equal(28u, BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4, 4)));
        Assert.Equal((byte)0x00, buf[8]);
        Assert.Equal((byte)0x14, buf[9]);
        Assert.Equal((ushort)state.Buttons, BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(10, 2)));
        Assert.Equal((byte)0xAA, buf[12]);
        Assert.Equal((byte)0x55, buf[13]);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(buf.AsSpan(14, 2)));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(buf.AsSpan(16, 2)));
        Assert.Equal((short)-1234, BinaryPrimitives.ReadInt16LittleEndian(buf.AsSpan(18, 2)));
        Assert.Equal((short)4321, BinaryPrimitives.ReadInt16LittleEndian(buf.AsSpan(20, 2)));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(5u)]
    public void BuildReportPacket_RejectsOutOfRangeSerialNo(uint invalidSerial)
    {
        var buf = new byte[28];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScpVBusPacketBuilder.BuildReportPacket(invalidSerial, GamepadState.Neutral, buf));
    }

    [Fact]
    public void BuildReportPacket_RejectsUndersizedBuffer()
    {
        var buf = new byte[20];
        Assert.Throws<ArgumentException>(() =>
            ScpVBusPacketBuilder.BuildReportPacket(1, GamepadState.Neutral, buf));
    }

    [Fact]
    public void BackendProbe_RunsNonDestructivelyAndReturnsConsistentResult()
    {
        var probe = BackendFactory.Probe();
        _output.WriteLine($"Probe Summary: {probe.Summary}");
        _output.WriteLine($"ViGEm={probe.ViGEmAvailable}, ScpVBus={probe.ScpVBusAvailable}, Path={probe.ScpVBusDevicePath}");

        Assert.False(string.IsNullOrWhiteSpace(probe.Summary));
        if (probe.ScpVBusAvailable)
        {
            Assert.False(string.IsNullOrWhiteSpace(probe.ScpVBusDevicePath));
            var responsive = ScpVBusBackend.TryQueryBusInfo(out var path, out var version, out var slots);
            _output.WriteLine($"Live ScpVBus IOCTL Query: responsive={responsive}, version=0x{version:X}, emptySlots={slots}, path={path}");
            Assert.True(responsive, "Expected live ScpVBus kernel driver to answer read-only VERSION/EMPTY_SLOTS IOCTLs.");
            Assert.InRange(slots, (byte)0, (byte)4);
        }

        using var hid = new HidDeviceService();
        var allDevices = hid.GetCurrentDevices(includeAllHid: true);
        _output.WriteLine($"HidSharp 2.x enumerated {allDevices.Count} HID devices with input reports.");
        foreach (var d in allDevices.Take(10))
        {
            _output.WriteLine($"  HID: {d.DisplayName}");
        }
    }
}
