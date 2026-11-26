using System.IO;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Hid;
using MFIGamepadFeeder.Core.Mapping;
using MFIGamepadFeeder.Core.Models;
using Xunit;

namespace MFIGamepadFeeder.Tests;

public sealed class HidReportMapperTests
{
    private const string LegacyNimbusJson = """
    {
      "MappingItems": [
        { "Type": 3, "InvertAxis": false, "ConvertAxis": false, "ButtonType": null, "AxisType": null },
        { "Type": 2, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 1, "AxisType": null },
        { "Type": 2, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 8, "AxisType": null },
        { "Type": 2, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 2, "AxisType": null },
        { "Type": 2, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 4, "AxisType": null },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 4096, "AxisType": null },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 8192, "AxisType": null },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 16384, "AxisType": null },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 32768, "AxisType": null },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 256, "AxisType": null },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 512, "AxisType": null },
        { "Type": 0, "InvertAxis": false, "ConvertAxis": false, "ButtonType": null, "AxisType": 4 },
        { "Type": 0, "InvertAxis": false, "ConvertAxis": false, "ButtonType": null, "AxisType": 5 },
        { "Type": 1, "InvertAxis": false, "ConvertAxis": false, "ButtonType": 16, "AxisType": null },
        { "Type": 0, "InvertAxis": false, "ConvertAxis": true, "ButtonType": null, "AxisType": 2 },
        { "Type": 0, "InvertAxis": false, "ConvertAxis": true, "ButtonType": null, "AxisType": 3 },
        { "Type": 0, "InvertAxis": false, "ConvertAxis": true, "ButtonType": null, "AxisType": 0 },
        { "Type": 0, "InvertAxis": false, "ConvertAxis": true, "ButtonType": null, "AxisType": 1 }
      ],
      "VirtualKeysItems": [
        { "SourceKeys": [512, 256, 16], "DestinationItem": 32 },
        { "SourceKeys": [256, 16], "DestinationItem": 64 },
        { "SourceKeys": [512, 16], "DestinationItem": 128 }
      ]
    }
    """;

    [Fact]
    public void LegacyNimbusMapping_MapsButtonsDpadTriggersAndSticksAtomically()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        var mapper = new HidReportMapper(mapping);

        // 18-byte Nimbus report: DpadUp=255, A=200, B=120, LT=128, RT=255, LX=+127, LY=-128 (128), RX=0, RY=64
        byte[] report = [0, 255, 0, 0, 0, 200, 120, 0, 0, 0, 0, 128, 255, 0, 127, 128, 0, 64];

        var ok = mapper.TryMapReport(report, out var state, out var diag, new MappingRuntimeOptions(Deadzone: 0.0));

        Assert.True(ok);
        Assert.Null(diag);
        Assert.Equal(XInputButtons.DpadUp | XInputButtons.A | XInputButtons.B, state.Buttons);
        Assert.Equal((byte)128, state.LeftTrigger);
        Assert.Equal((byte)255, state.RightTrigger);
        Assert.Equal(short.MaxValue, state.LeftThumbX);
        Assert.Equal(short.MinValue, state.LeftThumbY);
        Assert.Equal((short)0, state.RightThumbX);
        Assert.True(state.RightThumbY > 16000 && state.RightThumbY < 17000);
    }

    [Fact]
    public void EmptyMappingFile_DoesNotCrashAndReturnsDiagnostic()
    {
        // Exact contents of the user's Downloads\Nimbus.mfimapping
        var mapping = MappingSerializer.Deserialize("""{ "MappingItems": [], "VirtualKeysItems": [] }""");
        var mapper = new HidReportMapper(mapping);

        byte[] report = new byte[18];
        var ok = mapper.TryMapReport(report, out var state, out var diag);

        Assert.False(ok);
        Assert.Equal(GamepadState.Neutral, state);
        Assert.NotNull(diag);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(17)]
    public void ShortOrEmptyHidReport_NeverThrowsIndexOutOfRange(int reportLength)
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        var mapper = new HidReportMapper(mapping);

        var report = new byte[reportLength];
        Array.Fill(report, (byte)255);

        var ok = mapper.TryMapReport(report, out var state, out var diag);

        Assert.False(ok);
        Assert.Equal(GamepadState.Neutral, state);
        Assert.NotNull(diag);
    }

    [Fact]
    public void OversizedReport_MapsConfiguredBytesSafely()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        var mapper = new HidReportMapper(mapping);

        var report = new byte[24];
        report[5] = 255; // A
        report[14] = 127; // LX max positive

        var ok = mapper.TryMapReport(report, out var state, out _, new MappingRuntimeOptions(Deadzone: 0));

        Assert.True(ok);
        Assert.Equal(XInputButtons.A, state.Buttons);
        Assert.Equal(short.MaxValue, state.LeftThumbX);
    }

    [Fact]
    public void ReportIdFilter_SkipsMismatchedBatteryReportsQuietly()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        mapping.ReportId = 0x01;
        var mapper = new HidReportMapper(mapping);

        var batteryReport = new byte[18];
        batteryReport[0] = 0x02; // Wrong report ID
        batteryReport[5] = 255;

        var ok = mapper.TryMapReport(batteryReport, out var state, out var diag);
        Assert.False(ok);
        Assert.Null(diag);
        Assert.Equal(GamepadState.Neutral, state);

        batteryReport[0] = 0x01; // Matching report ID
        Assert.True(mapper.TryMapReport(batteryReport, out state, out _));
        Assert.Equal(XInputButtons.A, state.Buttons);
    }

    [Fact]
    public void SignedStickTwoComplement_HasNoOffByOneAt255()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        var mapper = new HidReportMapper(mapping);

        // Byte 255 in signed 8-bit two's complement is -1 (legacy bug mapped 255 -> 0!).
        var report = new byte[18];
        report[14] = 255; // LX = -1

        var ok = mapper.TryMapReport(report, out var state, out _, new MappingRuntimeOptions(Deadzone: 0.0));

        Assert.True(ok);
        Assert.Equal((short)-256, state.LeftThumbX);
    }

    [Fact]
    public void StickInversion_PreservesCenterAndHandlesMinMaxWithoutOverflow()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        // Enable InvertAxis on LY (index 15)
        mapping.MappingItems[15].InvertAxis = true;
        var mapper = new HidReportMapper(mapping);
        var opts = new MappingRuntimeOptions(Deadzone: 0.0);

        // Center (0) must remain 0 (legacy 1.0 - v bug turned 0 into +32767!)
        var center = new byte[18];
        Assert.True(mapper.TryMapReport(center, out var sCenter, out _, opts));
        Assert.Equal((short)0, sCenter.LeftThumbY);

        // Full negative (-128 -> byte 128) inverted must become +32767 (legacy overflowed short!)
        var min = new byte[18];
        min[15] = 128;
        Assert.True(mapper.TryMapReport(min, out var sMin, out _, opts));
        Assert.Equal(short.MaxValue, sMin.LeftThumbY);

        // Full positive (+127 -> byte 127) inverted must become -32768
        var max = new byte[18];
        max[15] = 127;
        Assert.True(mapper.TryMapReport(max, out var sMax, out _, opts));
        Assert.Equal(short.MinValue, sMax.LeftThumbY);
    }

    [Fact]
    public void TriggerInversion_InvertsWithinZeroTo255()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        mapping.MappingItems[11].InvertAxis = true; // LTrigger
        var mapper = new HidReportMapper(mapping);

        var r0 = new byte[18];
        r0[11] = 0;
        Assert.True(mapper.TryMapReport(r0, out var s0, out _));
        Assert.Equal((byte)255, s0.LeftTrigger);

        var r255 = new byte[18];
        r255[11] = 255;
        Assert.True(mapper.TryMapReport(r255, out var s255, out _));
        Assert.Equal((byte)0, s255.LeftTrigger);
    }

    [Fact]
    public void Deadzone_SuppressesSmallDriftAndStillReachesFullDeflection()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        var mapper = new HidReportMapper(mapping);
        var opts = new MappingRuntimeOptions(Deadzone: 0.10);

        // Small drift (+5 and -5 -> ~4%) suppressed to 0
        var drift = new byte[18];
        drift[14] = 5;   // +5
        drift[15] = 251; // -5
        Assert.True(mapper.TryMapReport(drift, out var sDrift, out _, opts));
        Assert.Equal((short)0, sDrift.LeftThumbX);
        Assert.Equal((short)0, sDrift.LeftThumbY);

        // Full tilt still reaches exact +32767 and -32768
        var full = new byte[18];
        full[14] = 127;
        full[15] = 128;
        Assert.True(mapper.TryMapReport(full, out var sFull, out _, opts));
        Assert.Equal(short.MaxValue, sFull.LeftThumbX);
        Assert.Equal(short.MinValue, sFull.LeftThumbY);
    }

    [Fact]
    public void VirtualKeys_LongestComboWinsRegardlessOfJsonOrder_AndDpadSourceIsClearedCleanly()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        // Deliberately reverse order so 2-button combos appear BEFORE 3-button combo!
        mapping.VirtualKeysItems =
        [
            new VirtualKeyMappingItem { SourceKeys = [XInputButtons.LBumper, XInputButtons.Start], DestinationItem = XInputButtons.LeftStick },
            new VirtualKeyMappingItem { SourceKeys = [XInputButtons.RBumper, XInputButtons.Start], DestinationItem = XInputButtons.RightStick },
            new VirtualKeyMappingItem { SourceKeys = [XInputButtons.RBumper, XInputButtons.LBumper, XInputButtons.Start], DestinationItem = XInputButtons.Back },
            new VirtualKeyMappingItem { SourceKeys = [XInputButtons.DpadUp, XInputButtons.Start], DestinationItem = XInputButtons.Guide },
        ];
        var mapper = new HidReportMapper(mapping);

        // Press LB(9) + RB(10) + Start(13) + A(5) -> must produce Back | A (NOT LeftStick | RB | A!)
        var report3 = new byte[18];
        report3[5] = 255;  // A
        report3[9] = 255;  // LB
        report3[10] = 255; // RB
        report3[13] = 255; // Start
        Assert.True(mapper.TryMapReport(report3, out var s3, out _));
        Assert.Equal(XInputButtons.Back | XInputButtons.A, s3.Buttons);

        // Press DpadUp(1) + Start(13) -> must produce Guide ONLY (DpadUp must be cleared, not XOR-flipped!)
        var reportDpad = new byte[18];
        reportDpad[1] = 255;  // DpadUp
        reportDpad[13] = 255; // Start
        Assert.True(mapper.TryMapReport(reportDpad, out var sDpad, out _));
        Assert.Equal(XInputButtons.Guide, sDpad.Buttons);
    }

    [Fact]
    public void ButtonThreshold_FiltersLightPressureOnAnalogMfiButtons()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        var mapper = new HidReportMapper(mapping);

        var report = new byte[18];
        report[5] = 20; // Light brush on A

        Assert.True(mapper.TryMapReport(report, out var sLow, out _, new MappingRuntimeOptions(ButtonThreshold: 30)));
        Assert.Equal(XInputButtons.None, sLow.Buttons);

        Assert.True(mapper.TryMapReport(report, out var sHit, out _, new MappingRuntimeOptions(ButtonThreshold: 20)));
        Assert.Equal(XInputButtons.A, sHit.Buttons);
    }

    [Fact]
    public void NimbusPlusBitmaskConfig_DecodesMultiplePackedButtonsFromSingleChannel()
    {
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MFIGamepadFeeder.App", "Configs", "NimbusPlus.mfimapping"));

        Assert.True(File.Exists(configPath), $"Expected bundled config at {configPath}");
        var mapping = MappingSerializer.LoadFromFile(configPath);
        var mapper = new HidReportMapper(mapping);

        Assert.Equal(9, mapper.MinimumRequiredReportLength);

        // 9-byte Nimbus+ report:
        // byte 1..4 sticks, 5..6 triggers, byte 7 = DpadUp(1) | A(16) | X(64) = 81, byte 8 = LB(1) | Start(64) = 65
        byte[] report = [0x01, 127, 0, 0, 128, 100, 200, 1 | 16 | 64, 1 | 64];

        var ok = mapper.TryMapReport(report, out var state, out var diag, new MappingRuntimeOptions(Deadzone: 0));
        Assert.True(ok);
        Assert.Null(diag);
        Assert.Equal(XInputButtons.DpadUp | XInputButtons.A | XInputButtons.X | XInputButtons.LBumper | XInputButtons.Start, state.Buttons);
        Assert.Equal((byte)100, state.LeftTrigger);
        Assert.Equal((byte)200, state.RightTrigger);
        Assert.Equal(short.MaxValue, state.LeftThumbX);
        Assert.Equal(short.MinValue, state.RightThumbY);
    }

    [Fact]
    public void KnownMfiDevices_IdentifiesSteelSeriesNimbusAndNimbusPlus()
    {
        var nimbus = KnownMfiDevices.Match(0x0111, 0x1420);
        Assert.NotNull(nimbus);
        Assert.Equal("SteelSeries Nimbus", nimbus.DisplayName);
        Assert.Equal("Nimbus.mfimapping", nimbus.RecommendedConfigFileName);

        var nimbusPlus = KnownMfiDevices.Match(0x0111, 0x1421);
        Assert.NotNull(nimbusPlus);
        Assert.Equal("NimbusPlus.mfimapping", nimbusPlus.RecommendedConfigFileName);
    }

    [Fact]
    public void VirtualKeys_DoNotAccidentallyChainSynthesizedOutputs()
    {
        var mapping = MappingSerializer.Deserialize(LegacyNimbusJson);
        mapping.VirtualKeysItems =
        [
            new VirtualKeyMappingItem { SourceKeys = [XInputButtons.A, XInputButtons.B], DestinationItem = XInputButtons.X },
            new VirtualKeyMappingItem { SourceKeys = [XInputButtons.X, XInputButtons.Y], DestinationItem = XInputButtons.Start },
        ];
        var mapper = new HidReportMapper(mapping);

        // Press physical A(5) + B(6) + Y(8) (X is NOT physically pressed)
        var report = new byte[18];
        report[5] = 255;
        report[6] = 255;
        report[8] = 255;

        Assert.True(mapper.TryMapReport(report, out var state, out _));
        // A+B synthesizes X, Y remains pressed; must NOT chain synthesized X + Y into Start!
        Assert.Equal(XInputButtons.X | XInputButtons.Y, state.Buttons);
    }

    [Fact]
    public void BundledNimbusMapping_LoadsAndMatchesLegacyInstalledConfigLayout()
    {
        var bundledPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MFIGamepadFeeder.App", "Configs", "Nimbus.mfimapping"));

        var bundled = MappingSerializer.LoadFromFile(bundledPath);
        Assert.Equal(18, bundled.MappingItems.Count);
        Assert.Equal(3, bundled.VirtualKeysItems.Count);

        var mapper = new HidReportMapper(bundled);
        Assert.Equal(18, mapper.MinimumRequiredReportLength);

        var installedLegacyPath = @"C:\Program Files (x86)\MFIGamepadFeeder\Configs\Nimbus.mfimapping";
        if (File.Exists(installedLegacyPath))
        {
            var legacy = MappingSerializer.LoadFromFile(installedLegacyPath);
            Assert.Equal(bundled.MappingItems.Count, legacy.MappingItems.Count);
            for (var i = 0; i < bundled.MappingItems.Count; i++)
            {
                Assert.Equal(legacy.MappingItems[i].Type, bundled.MappingItems[i].Type);
                Assert.Equal(legacy.MappingItems[i].ButtonType, bundled.MappingItems[i].ButtonType);
                Assert.Equal(legacy.MappingItems[i].AxisType, bundled.MappingItems[i].AxisType);
                Assert.Equal(legacy.MappingItems[i].ConvertAxis, bundled.MappingItems[i].ConvertAxis);
            }
        }
    }
}
