namespace MFIGamepadFeeder.Core.Hid;

public sealed record HidDeviceInfo(
    string DevicePath,
    int VendorId,
    int ProductId,
    int ReleaseNumberBcd,
    string Manufacturer,
    string ProductName,
    string SerialNumber,
    int MaxInputReportLength,
    KnownMfiProfile? KnownProfile,
    bool IsHidGamepadUsage = false)
{
    public bool IsKnownMfi => KnownProfile is not null;

    public string DisplayName
    {
        get
        {
            if (KnownProfile is not null)
            {
                return $"[MFi] {KnownProfile.DisplayName} (VID:0x{VendorId:X4} PID:0x{ProductId:X4}, {MaxInputReportLength}B)";
            }

            var label = string.Join(' ', new[] { Manufacturer, ProductName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                label = IsHidGamepadUsage ? "HID Gamepad" : "HID Device";
            }

            var prefix = IsHidGamepadUsage ? "[Gamepad] " : string.Empty;
            return $"{prefix}{label} (VID:0x{VendorId:X4} PID:0x{ProductId:X4}, {MaxInputReportLength}B)";
        }
    }

    public override string ToString() => DisplayName;
}
