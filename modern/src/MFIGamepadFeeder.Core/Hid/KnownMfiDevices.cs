namespace MFIGamepadFeeder.Core.Hid;

public sealed record KnownMfiProfile(
    int VendorId,
    int ProductId,
    string DisplayName,
    string RecommendedConfigFileName);

/// <summary>
/// Recognizes known MFi Bluetooth gamepads by USB/BT VID/PID even when Windows Bluetooth HID
/// leaves Manufacturer and ProductName blank (a frequent issue on SteelSeries Nimbus).
/// </summary>
public static class KnownMfiDevices
{
    public const int SteelSeriesVendorId = 0x0111;

    private static readonly KnownMfiProfile[] Profiles =
    [
        new(SteelSeriesVendorId, 0x1420, "SteelSeries Nimbus", "Nimbus.mfimapping"),
        new(SteelSeriesVendorId, 0x1421, "SteelSeries Nimbus+", "NimbusPlus.mfimapping"),
        new(SteelSeriesVendorId, 0x1422, "SteelSeries Nimbus+ (Rev 2)", "NimbusPlus.mfimapping"),
        new(SteelSeriesVendorId, 0x1418, "SteelSeries Stratus", "Nimbus.mfimapping"),
        new(SteelSeriesVendorId, 0x1419, "SteelSeries Stratus XL", "Nimbus.mfimapping"),
        new(SteelSeriesVendorId, 0x141E, "SteelSeries Stratus XL (MFi)", "Nimbus.mfimapping"),
        new(SteelSeriesVendorId, 0x1431, "SteelSeries Stratus Duo", "Nimbus.mfimapping"),
        new(0x0F0D, 0x0090, "HORIPAD Ultimate (MFi)", "Nimbus.mfimapping"),
        new(0x27F8, 0x0BBE, "Gamevice MFi Controller", "Nimbus.mfimapping"),
    ];

    public static KnownMfiProfile? Match(int vendorId, int productId)
    {
        foreach (var profile in Profiles)
        {
            if (profile.VendorId == vendorId && profile.ProductId == productId)
            {
                return profile;
            }
        }

        return null;
    }

    public static bool IsLikelyGamepad(int vendorId, int productId, string? productName, int maxInputReportLength)
    {
        if (Match(vendorId, productId) is not null)
        {
            return true;
        }

        if (maxInputReportLength is < 4 or > 64)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            var lower = productName.ToLowerInvariant();
            if (lower.Contains("nimbus") || lower.Contains("gamepad") || lower.Contains("controller") || lower.Contains("stratus") || lower.Contains("mfi"))
            {
                return true;
            }
        }

        return false;
    }
}
