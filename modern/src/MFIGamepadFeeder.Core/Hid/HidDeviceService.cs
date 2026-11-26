using HidSharp;

namespace MFIGamepadFeeder.Core.Hid;

/// <summary>
/// Event-driven HID device enumerator powered by HidSharp 2.x DeviceList.Local.Changed.
/// Replaces the legacy 10-second polling thread that leaked threads and never removed unplugged devices.
/// </summary>
public sealed class HidDeviceService : IDisposable
{
    // Standard USB HID Generic Desktop Usage Page (0x0001) usages for game controllers.
    private const uint UsageGenericDesktopJoystick = 0x00010004;
    private const uint UsageGenericDesktopGamepad = 0x00010005;
    private const uint UsageGenericDesktopMultiAxis = 0x00010008;

    private bool _disposed;

    public event Action? DeviceListChanged;
    public event Action<IReadOnlyList<HidDeviceInfo>>? DevicesChanged;

    public HidDeviceService()
    {
        DeviceList.Local.Changed += OnDeviceListChanged;
    }

    public IReadOnlyList<HidDeviceInfo> GetCurrentDevices(bool includeAllHid = false)
    {
        var results = new List<HidDeviceInfo>();

        foreach (var dev in DeviceList.Local.GetHidDevices())
        {
            int maxIn;
            try
            {
                maxIn = dev.GetMaxInputReportLength();
            }
            catch
            {
                continue;
            }

            if (maxIn <= 0)
            {
                continue;
            }

            // Never include our own virtual Xbox 360 controller unless explicitly requested via Show All HID.
            if (!includeAllHid && dev.VendorID == 0x045E && dev.ProductID == 0x028E)
            {
                continue;
            }

            var manufacturer = TryRead(() => dev.GetManufacturer());
            var productName = TryRead(() => dev.GetProductName());
            if (string.IsNullOrWhiteSpace(productName))
            {
                productName = TryRead(() => dev.GetFriendlyName());
            }

            var serial = TryRead(() => dev.GetSerialNumber());
            var known = KnownMfiDevices.Match(dev.VendorID, dev.ProductID);
            var isHidGamepad = HasGamepadOrJoystickUsage(dev);

            if (!includeAllHid && !isHidGamepad && !KnownMfiDevices.IsLikelyGamepad(dev.VendorID, dev.ProductID, productName, maxIn))
            {
                continue;
            }

            results.Add(new HidDeviceInfo(
                dev.DevicePath,
                dev.VendorID,
                dev.ProductID,
                dev.ReleaseNumberBcd,
                manufacturer,
                productName,
                serial,
                maxIn,
                known,
                isHidGamepad));
        }

        // Put known MFi controllers (SteelSeries Nimbus / Nimbus+) first, then HID gamepads, then alphabetically.
        results.Sort((a, b) =>
        {
            var mfiCmp = b.IsKnownMfi.CompareTo(a.IsKnownMfi);
            if (mfiCmp != 0) return mfiCmp;
            var gpCmp = b.IsHidGamepadUsage.CompareTo(a.IsHidGamepadUsage);
            if (gpCmp != 0) return gpCmp;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }

    internal static bool HasGamepadOrJoystickUsage(HidDevice dev)
    {
        try
        {
            var descriptor = dev.GetReportDescriptor();
            foreach (var item in descriptor.DeviceItems)
            {
                foreach (var usage in item.Usages.GetAllValues())
                {
                    if (usage is UsageGenericDesktopJoystick or UsageGenericDesktopGamepad or UsageGenericDesktopMultiAxis)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Some HID collections deny descriptor reads; fall back to VID/PID & name matching.
        }

        return false;
    }

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DeviceListChanged?.Invoke();

            // Only perform an eager full HID scan if someone subscribed to the legacy snapshot event.
            var snapshotHandler = DevicesChanged;
            if (snapshotHandler is not null)
            {
                snapshotHandler.Invoke(GetCurrentDevices(includeAllHid: true));
            }
        }
        catch
        {
            // Subscribers handle their own dispatch/errors.
        }
    }

    private static string TryRead(Func<string> getter)
    {
        try
        {
            return getter()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DeviceList.Local.Changed -= OnDeviceListChanged;
    }
}
