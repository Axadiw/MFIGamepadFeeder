using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Backends;

/// <summary>
/// Direct atomic IOCTL backend for an already-installed ScpVBus.sys driver (legacy fallback when ViGEmBus is not installed).
/// Eliminates vGenInterface.dll, reduces kernel transitions from 9 IOCTLs/report to 1 atomic IOCTL/report,
/// eliminates D-pad flicker, uses the full 10-byte feedback buffer matching ScpVBus.sys (rumble + LED + started flag),
/// polls ISDEVPLUGGED / PDO-started instead of blind sleeps, and avoids Bus_CleanUp handle-close hazards.
/// </summary>
public sealed class ScpVBusBackend : IVirtualGamepadBackend
{
    private const int ErrorInvalidParameter = 87;

    // ScpVBus.sys Bus_CleanUp (IRP_MJ_CLEANUP) unplugs ALL PDOs owned by CURRENT_PROCESS_ID()
    // whenever ANY handle to the FDO in this process is closed. Track active backend instances so
    // static read-only probes reuse an existing open handle instead of opening & closing a second one.
    private static readonly object ActiveRegistryLock = new();
    private static readonly List<ScpVBusBackend> ActiveInstances = [];

    private readonly object _ioLock = new();
    private readonly byte[] _reportInBuf = new byte[ScpVBusPacketBuilder.ReportPacketSize];
    private readonly byte[] _reportOutBuf = new byte[ScpVBusPacketBuilder.FeedbackBufferSize];
    private readonly byte[] _controlBuf = new byte[ScpVBusPacketBuilder.ControlPacketSize];
    private readonly byte[] _isPluggedInBuf = new byte[ScpVBusPacketBuilder.IsDevPluggedBufferSize];
    private readonly byte[] _isPluggedOutBuf = new byte[ScpVBusPacketBuilder.IsDevPluggedBufferSize];

    private SafeFileHandle? _busHandle;
    private string? _openedDevicePath;
    private bool _disposed;

    public string Name => "ScpVBus (Legacy Atomic IOCTL)";
    public bool IsLegacyDriver => true;

    public bool IsAvailable() => TryQueryBusInfo(out _, out _, out _);

    /// <summary>
    /// Non-destructive read-only probe against ScpVBus.sys verifying handle open + IOCTL_BUSENUM_VERSION &amp; EMPTY_SLOTS.
    /// Safely reuses an active in-process ScpVBusBackend handle when one is open so Bus_CleanUp never unplugs live slots.
    /// </summary>
    public static bool TryQueryBusInfo(out string? devicePath, out uint busVersion, out byte emptySlots)
    {
        busVersion = 0;
        emptySlots = 0;

        // If this process already holds an open ScpVBus handle, query through it without closing it!
        lock (ActiveRegistryLock)
        {
            foreach (var instance in ActiveInstances)
            {
                if (instance.TryQueryOnOpenHandle(out devicePath, out busVersion, out emptySlots))
                {
                    return true;
                }
            }
        }

        devicePath = FindBusDevicePath();
        if (devicePath is null)
        {
            return false;
        }

        using var handle = NativeMethods.CreateFile(
            devicePath,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return false;
        }

        return QueryBusInfoInternal(handle, out busVersion, out emptySlots);
    }

    /// <summary>
    /// Non-destructive read-only query checking whether a given ScpVBus slot (1..4) is currently plugged in.
    /// </summary>
    public static bool TryQuerySlotPlugged(uint slotIndex, out bool isPlugged)
    {
        isPlugged = false;
        if (slotIndex is < 1 or > 4)
        {
            return false;
        }

        lock (ActiveRegistryLock)
        {
            foreach (var instance in ActiveInstances)
            {
                if (instance.TryQuerySlotOnOpenHandle(slotIndex, out isPlugged))
                {
                    return true;
                }
            }
        }

        var path = FindBusDevicePath();
        if (path is null)
        {
            return false;
        }

        using var handle = NativeMethods.CreateFile(
            path,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return false;
        }

        var inBuf = new byte[ScpVBusPacketBuilder.IsDevPluggedBufferSize];
        var outBuf = new byte[ScpVBusPacketBuilder.IsDevPluggedBufferSize];
        ScpVBusPacketBuilder.BuildIsDevPluggedInput(slotIndex, inBuf);

        if (!NativeMethods.DeviceIoControl(
                handle,
                ScpVBusPacketBuilder.IoctlIsDevPlugged,
                inBuf,
                (uint)inBuf.Length,
                outBuf,
                (uint)outBuf.Length,
                out _,
                IntPtr.Zero))
        {
            return false;
        }

        isPlugged = outBuf[0] != 0;
        return true;
    }

    private bool TryQueryOnOpenHandle(out string? devicePath, out uint busVersion, out byte emptySlots)
    {
        lock (_ioLock)
        {
            if (_busHandle is { IsInvalid: false, IsClosed: false } && _openedDevicePath is not null)
            {
                devicePath = _openedDevicePath;
                return QueryBusInfoInternal(_busHandle, out busVersion, out emptySlots);
            }
        }

        devicePath = null;
        busVersion = 0;
        emptySlots = 0;
        return false;
    }

    private bool TryQuerySlotOnOpenHandle(uint slotIndex, out bool isPlugged)
    {
        lock (_ioLock)
        {
            if (_busHandle is { IsInvalid: false, IsClosed: false })
            {
                return TryIsDevicePluggedInLocked(slotIndex, out isPlugged);
            }
        }

        isPlugged = false;
        return false;
    }

    private static bool QueryBusInfoInternal(SafeFileHandle handle, out uint busVersion, out byte emptySlots)
    {
        busVersion = 0;
        emptySlots = 0;

        var versionBuf = new byte[4];
        var okVer = NativeMethods.DeviceIoControl(
            handle,
            ScpVBusPacketBuilder.IoctlVersion,
            null,
            0,
            versionBuf,
            (uint)versionBuf.Length,
            out _,
            IntPtr.Zero);

        if (okVer)
        {
            busVersion = BinaryPrimitives.ReadUInt32LittleEndian(versionBuf);
        }

        var slotsBuf = new byte[1];
        var okSlots = NativeMethods.DeviceIoControl(
            handle,
            ScpVBusPacketBuilder.IoctlEmptySlots,
            null,
            0,
            slotsBuf,
            (uint)slotsBuf.Length,
            out _,
            IntPtr.Zero);

        if (okSlots)
        {
            emptySlots = slotsBuf[0];
        }

        return okVer || okSlots;
    }

    public static string? FindBusDevicePath()
    {
        var guid = ScpVBusPacketBuilder.DeviceInterfaceGuid;
        var devInfo = NativeMethods.SetupDiGetClassDevs(
            ref guid,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

        if (devInfo == IntPtr.Zero || devInfo == NativeMethods.InvalidHandleValue)
        {
            return null;
        }

        try
        {
            for (uint memberIndex = 0; ; memberIndex++)
            {
                var ifaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>(),
                };

                if (!NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref guid, memberIndex, ref ifaceData))
                {
                    return null;
                }

                NativeMethods.SetupDiGetDeviceInterfaceDetail(
                    devInfo,
                    ref ifaceData,
                    IntPtr.Zero,
                    0,
                    out var requiredSize,
                    IntPtr.Zero);

                if (requiredSize == 0)
                {
                    continue;
                }

                var detailBuf = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    // Win32 ABI requirement for SP_DEVICE_INTERFACE_DETAIL_DATA_W:
                    // cbSize = 8 on 64-bit Windows, 6 on 32-bit Windows.
                    Marshal.WriteInt32(detailBuf, IntPtr.Size == 8 ? 8 : 6);

                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                            devInfo,
                            ref ifaceData,
                            detailBuf,
                            requiredSize,
                            out _,
                            IntPtr.Zero))
                    {
                        continue;
                    }

                    var path = Marshal.PtrToStringUni(IntPtr.Add(detailBuf, 4));
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuf);
                }
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(devInfo);
        }
    }

    public IVirtualController ConnectController(uint slotIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (slotIndex is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Controller slot must be between 1 and 4.");
        }

        lock (_ioLock)
        {
            EnsureOpenLocked();

            // Check whether a ghost virtual controller already occupies this slot before unplugging.
            if (TryIsDevicePluggedInLocked(slotIndex, out var alreadyPlugged) && alreadyPlugged)
            {
                TryUnplugLocked(slotIndex, force: true);

                // Poll ISDEVPLUGGED while Windows PnP processes IRP_MN_SURPRISE_REMOVAL / REMOVE_DEVICE.
                var unplugWatch = Stopwatch.StartNew();
                while (unplugWatch.ElapsedMilliseconds < 1500)
                {
                    Thread.Sleep(25);
                    if (TryIsDevicePluggedInLocked(slotIndex, out var stillPlugged) && !stillPlugged)
                    {
                        break;
                    }
                }
            }

            ScpVBusPacketBuilder.BuildPlugInPacket(slotIndex, _controlBuf);

            // Retry briefly if Windows PnP is still completing surprise removal of an old PDO (error 87).
            var pluggedIn = false;
            var lastErr = 0;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (SendIoctlLocked(ScpVBusPacketBuilder.IoctlPlugIn, _controlBuf, null))
                {
                    pluggedIn = true;
                    break;
                }

                lastErr = Marshal.GetLastWin32Error();
                if (lastErr != ErrorInvalidParameter)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            if (!pluggedIn)
            {
                throw new Win32Exception(lastErr, $"ScpVBus failed to plug in virtual controller slot {slotIndex} (Win32 error {lastErr}).");
            }

            // Wait for xusb22.sys to start the virtual Xbox 360 PDO (indicated by feedback byte 9 != 0).
            var startWatch = Stopwatch.StartNew();
            while (startWatch.ElapsedMilliseconds < 1500)
            {
                if (SendReportLocked(slotIndex, GamepadState.Neutral, out var isStarted, out _) && isStarted)
                {
                    break;
                }

                Thread.Sleep(25);
            }
        }

        var controller = new ScpVirtualController(this, slotIndex);
        controller.Reset();
        return controller;
    }

    private void EnsureOpenLocked()
    {
        if (_busHandle is { IsInvalid: false, IsClosed: false })
        {
            return;
        }

        var path = FindBusDevicePath()
            ?? throw new InvalidOperationException("ScpVBus driver is not installed or not running.");

        // Synchronous handle (no FILE_FLAG_OVERLAPPED when calling synchronous DeviceIoControl).
        var handle = NativeMethods.CreateFile(
            path,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var err = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(err, $"Failed to open ScpVBus device '{path}' (Win32 error {err}).");
        }

        _busHandle = handle;
        _openedDevicePath = path;

        lock (ActiveRegistryLock)
        {
            if (!ActiveInstances.Contains(this))
            {
                ActiveInstances.Add(this);
            }
        }
    }

    internal bool SendReport(uint slotIndex, in GamepadState state, out int lastError)
    {
        lock (_ioLock)
        {
            return SendReportLocked(slotIndex, state, out _, out lastError);
        }
    }

    private bool SendReportLocked(uint slotIndex, in GamepadState state, out bool isDeviceStarted, out int lastError)
    {
        isDeviceStarted = false;
        lastError = 0;
        if (_busHandle is null || _busHandle.IsInvalid || _busHandle.IsClosed)
        {
            lastError = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        ScpVBusPacketBuilder.BuildReportPacket(slotIndex, state, _reportInBuf);
        Array.Clear(_reportOutBuf, 0, _reportOutBuf.Length);

        var ok = SendIoctlLocked(ScpVBusPacketBuilder.IoctlReport, _reportInBuf, _reportOutBuf);
        if (ok)
        {
            isDeviceStarted = ScpVBusPacketBuilder.IsFeedbackDeviceStarted(_reportOutBuf);
        }
        else
        {
            lastError = Marshal.GetLastWin32Error();
        }

        return ok;
    }

    internal bool TryUnplug(uint slotIndex, bool force)
    {
        lock (_ioLock)
        {
            return TryUnplugLocked(slotIndex, force);
        }
    }

    private bool TryUnplugLocked(uint slotIndex, bool force)
    {
        if (_busHandle is null || _busHandle.IsInvalid || _busHandle.IsClosed)
        {
            return false;
        }

        ScpVBusPacketBuilder.BuildUnPlugPacket(slotIndex, force, _controlBuf);
        return SendIoctlLocked(ScpVBusPacketBuilder.IoctlUnPlug, _controlBuf, null);
    }

    private bool TryIsDevicePluggedInLocked(uint slotIndex, out bool isPlugged)
    {
        isPlugged = false;
        if (_busHandle is null || _busHandle.IsInvalid || _busHandle.IsClosed)
        {
            return false;
        }

        ScpVBusPacketBuilder.BuildIsDevPluggedInput(slotIndex, _isPluggedInBuf);
        Array.Clear(_isPluggedOutBuf, 0, _isPluggedOutBuf.Length);

        if (!SendIoctlLocked(ScpVBusPacketBuilder.IoctlIsDevPlugged, _isPluggedInBuf, _isPluggedOutBuf))
        {
            return false;
        }

        isPlugged = _isPluggedOutBuf[0] != 0;
        return true;
    }

    private bool SendIoctlLocked(uint ioctlCode, byte[] input, byte[]? output)
    {
        if (_busHandle is null || _busHandle.IsInvalid || _busHandle.IsClosed)
        {
            return false;
        }

        return NativeMethods.DeviceIoControl(
            _busHandle,
            ioctlCode,
            input,
            (uint)input.Length,
            output,
            (uint)(output?.Length ?? 0),
            out _,
            IntPtr.Zero);
    }

    public void Dispose()
    {
        lock (ActiveRegistryLock)
        {
            ActiveInstances.Remove(this);
        }

        lock (_ioLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _busHandle?.Dispose();
            _busHandle = null;
            _openedDevicePath = null;
        }
    }

    private sealed class ScpVirtualController : IVirtualController
    {
        private readonly ScpVBusBackend _backend;
        private readonly object _lock = new();
        private bool _disposed;

        public ScpVirtualController(ScpVBusBackend backend, uint slotIndex)
        {
            _backend = backend;
            SlotIndex = slotIndex;
        }

        public string BackendName => "ScpVBus (Legacy Atomic IOCTL)";
        public uint SlotIndex { get; }

        public void SubmitState(in GamepadState state)
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!_backend.SendReport(SlotIndex, state, out var err))
                {
                    throw new Win32Exception(err, $"ScpVBus report submission failed for slot {SlotIndex} (Win32 error {err}).");
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _backend.SendReport(SlotIndex, GamepadState.Neutral, out _);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    _backend.SendReport(SlotIndex, GamepadState.Neutral, out _);
                    if (!_backend.TryUnplug(SlotIndex, force: false))
                    {
                        _backend.TryUnplug(SlotIndex, force: true);
                    }
                }
                catch
                {
                    // Best-effort cleanup on shutdown.
                }

                _disposed = true;
            }
        }
    }

    private static class NativeMethods
    {
        public static readonly IntPtr InvalidHandleValue = new(-1);

        public const uint DIGCF_PRESENT = 0x00000002;
        public const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public UIntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            SafeFileHandle deviceHandle,
            uint ioControlCode,
            byte[]? inBuffer,
            uint inBufferSize,
            byte[]? outBuffer,
            uint outBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);
    }
}
