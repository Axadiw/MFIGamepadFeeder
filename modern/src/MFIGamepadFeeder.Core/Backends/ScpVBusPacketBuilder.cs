using System.Buffers.Binary;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Backends;

/// <summary>
/// Builds exact binary IOCTL buffers matching nefarius/ScpVBus v1.7.1.2 (Common/ScpVBus.h &amp; Common/IoCtrl.h).
/// Allows atomic 1-IOCTL-per-frame updates directly to ScpVBus.sys without vGenInterface.dll's 9-IOCTL bottleneck.
/// </summary>
public static class ScpVBusPacketBuilder
{
    public const int ControlPacketSize = 16;
    public const int ReportPacketSize = 28;
    // Matches ScpVBus/bus/busenum.h: RUMBLE_SIZE(8) + LEDNUM_SIZE(1) + STATUS_SIZE(1) = 10.
    // ScpVBus/bus/busenum.c sets Irp->IoStatus.Information = 10 unconditionally on REPORT_HARDWARE.
    public const int FeedbackBufferSize = 10;
    public const int FeedbackRumbleOffset = 0;
    public const int FeedbackLedOffset = 8;
    public const int FeedbackStartedStatusOffset = 9;
    public const int IsDevPluggedBufferSize = 4;

    // Exact CTL_CODE values verified against installed ScpVBus.sys v1.7.1.2 & vGenInterface.dll:
    // FILE_DEVICE_BUS_EXTENDER = 0x2A, METHOD_BUFFERED = 0, BASE = 0x801
    public const uint IoctlPlugIn = 0x002AA004;
    public const uint IoctlUnPlug = 0x002AA008;
    public const uint IoctlEject = 0x002AA00C;
    public const uint IoctlReport = 0x002AE010;
    public const uint IoctlIsDevPlugged = 0x002AE404;
    public const uint IoctlEmptySlots = 0x002AE408;
    public const uint IoctlProcId = 0x002AE40C;
    public const uint IoctlVersion = 0x002AE410;

    public static readonly Guid DeviceInterfaceGuid =
        new(0xf679f562, 0x3164, 0x42ce, 0xa4, 0xdb, 0xe7, 0xdd, 0xbe, 0x72, 0x39, 0x09);

    public static void BuildPlugInPacket(uint serialNo, Span<byte> destination)
    {
        ValidateSerialAndSpan(serialNo, destination, ControlPacketSize);
        destination.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(destination[0..4], ControlPacketSize);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..8], serialNo);
    }

    public static void BuildUnPlugPacket(uint serialNo, bool force, Span<byte> destination)
    {
        if (serialNo > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(serialNo), "SerialNo must be 0 (all) or 1..4.");
        }

        if (destination.Length < ControlPacketSize)
        {
            throw new ArgumentException($"Buffer must be at least {ControlPacketSize} bytes.", nameof(destination));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(destination[0..4], ControlPacketSize);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..8], serialNo);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..12], force ? 1u : 0u);
    }

    /// <summary>
    /// Encodes BUSENUM_REPORT_HARDWARE (28 bytes) containing a full 20-byte XUSB Xbox 360 input report.
    /// Buttons + D-Pad + both triggers + all 4 stick axes are committed atomically in ONE DeviceIoControl call.
    /// </summary>
    public static void BuildReportPacket(uint serialNo, in GamepadState state, Span<byte> destination)
    {
        ValidateSerialAndSpan(serialNo, destination, ReportPacketSize);
        destination.Clear();

        BinaryPrimitives.WriteUInt32LittleEndian(destination[0..4], ReportPacketSize);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..8], serialNo);
        destination[8] = 0x00; // XUSB Input Report type
        destination[9] = 0x14; // XUSB Input Report length (20 bytes)

        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..12], (ushort)state.Buttons);
        destination[12] = state.LeftTrigger;
        destination[13] = state.RightTrigger;
        BinaryPrimitives.WriteInt16LittleEndian(destination[14..16], state.LeftThumbX);
        BinaryPrimitives.WriteInt16LittleEndian(destination[16..18], state.LeftThumbY);
        BinaryPrimitives.WriteInt16LittleEndian(destination[18..20], state.RightThumbX);
        BinaryPrimitives.WriteInt16LittleEndian(destination[20..22], state.RightThumbY);
    }

    public static void BuildIsDevPluggedInput(uint serialNo, Span<byte> destination)
    {
        ValidateSerialAndSpan(serialNo, destination, IsDevPluggedBufferSize);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[0..4], serialNo);
    }

    public static bool IsFeedbackDeviceStarted(ReadOnlySpan<byte> feedback)
    {
        return feedback.Length >= FeedbackBufferSize && feedback[FeedbackStartedStatusOffset] != 0;
    }

    private static void ValidateSerialAndSpan(uint serialNo, Span<byte> destination, int requiredLength)
    {
        if (serialNo is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(serialNo), "SerialNo must be between 1 and 4.");
        }

        if (destination.Length < requiredLength)
        {
            throw new ArgumentException($"Buffer must be at least {requiredLength} bytes.", nameof(destination));
        }
    }
}
