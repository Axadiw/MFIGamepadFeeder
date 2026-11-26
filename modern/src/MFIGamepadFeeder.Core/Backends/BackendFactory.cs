namespace MFIGamepadFeeder.Core.Backends;

public static class BackendFactory
{
    public static BackendProbeResult Probe()
    {
        using var vigem = new ViGEmBackend();
        var vigemOk = vigem.IsAvailable();
        var scpResponsive = ScpVBusBackend.TryQueryBusInfo(out var scpPath, out var scpVer, out var emptySlots);
        var scpOk = scpResponsive;

        var scpDetail = scpResponsive
            ? $"v0x{scpVer:X} ({emptySlots}/4 free slots)"
            : "device present but did not answer IOCTL query";

        var summary = (vigemOk, scpOk, scpPath is not null) switch
        {
            (true, _, _) => "ViGEmBus detected (Recommended — modern Windows 11/HVCI-safe driver).",
            (false, true, _) => $"Legacy ScpVBus detected — {scpDetail} (using direct atomic IOCTL fallback). Installing ViGEmBus v1.22.0 is recommended for Windows 11 stability.",
            (false, false, true) => $"Legacy ScpVBus {scpDetail}. Restarting Windows or installing ViGEmBus v1.22.0 is recommended.",
            (false, false, false) => "No virtual Xbox 360 bus driver found. Please install ViGEmBus v1.22.0 from github.com/nefarius/ViGEmBus/releases.",
        };

        return new BackendProbeResult(vigemOk, scpOk, scpPath, summary);
    }

    public static IVirtualGamepadBackend Create(BackendPreference preference = BackendPreference.Auto)
    {
        switch (preference)
        {
            case BackendPreference.ViGEmBus:
                var vigem = new ViGEmBackend();
                if (!vigem.IsAvailable())
                {
                    vigem.Dispose();
                    throw new InvalidOperationException("ViGEmBus is not installed. Download ViGEmBus v1.22.0 from https://github.com/nefarius/ViGEmBus/releases.");
                }
                return vigem;

            case BackendPreference.ScpVBus:
                var scp = new ScpVBusBackend();
                if (!scp.IsAvailable())
                {
                    scp.Dispose();
                    throw new InvalidOperationException("ScpVBus driver is not installed or running.");
                }
                return scp;

            default:
                var preferred = new ViGEmBackend();
                if (preferred.IsAvailable())
                {
                    return preferred;
                }
                preferred.Dispose();

                var fallback = new ScpVBusBackend();
                if (fallback.IsAvailable())
                {
                    return fallback;
                }
                fallback.Dispose();

                throw new InvalidOperationException(
                    "No virtual Xbox 360 bus driver found (neither ViGEmBus nor ScpVBus). Install ViGEmBus v1.22.0 from https://github.com/nefarius/ViGEmBus/releases.");
        }
    }
}
