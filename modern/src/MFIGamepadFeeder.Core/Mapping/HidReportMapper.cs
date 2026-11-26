using System.Numerics;
using MFIGamepadFeeder.Core.Configuration;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Mapping;

public sealed record MappingRuntimeOptions(
    double Deadzone = 0.06,
    byte ButtonThreshold = 1,
    bool InvertLeftY = false,
    bool InvertRightY = false);

/// <summary>
/// Pure, bounds-safe HID-report to Xbox 360 state mapper.
/// Fixes upstream bugs:
/// - Bounds-checks every byte index and rejects short/unrelated HID reports instead of throwing IndexOutOfRangeException.
/// - Accurate signed 8-bit two's-complement stick normalization (no off-by-one at byte 255).
/// - Proper signed stick inversion (-v around 0) vs unsigned trigger inversion (1 - v in [0,1]).
/// - Smooth rescaled axial deadzone to eliminate Nimbus stick drift.
/// - Unified Buttons + D-Pad bitmask (eliminates D-pad flicker).
/// - Deterministic Virtual Keys (most-specific combo first; clears source bits cleanly from both buttons and D-pad).
/// - Full compatibility with both original Nimbus pressure-byte configs and PR #73 Nimbus+ bitmask configs.
/// </summary>
public sealed class HidReportMapper
{
    private readonly GamepadMapping _mapping;
    private readonly List<CompiledVirtualKey> _virtualKeys;
    private readonly int _minRequiredReportLength;

    private readonly record struct CompiledVirtualKey(
        XInputButtons SourceMask,
        XInputButtons Destination,
        int KeyCount);

    public HidReportMapper(GamepadMapping mapping)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        var maxIndex = -1;
        for (var i = 0; i < _mapping.MappingItems.Count; i++)
        {
            var item = _mapping.MappingItems[i];
            if (item is null || item.Type == GamepadMappingItemType.Empty)
            {
                continue;
            }

            var idx = item.CustomIndex ?? i;
            if (idx >= 0 && idx > maxIndex)
            {
                maxIndex = idx;
            }
        }

        _minRequiredReportLength = maxIndex + 1;

        // Compile virtual key combinations ordered by specificity (highest popcount first),
        // so 3-key combos like LB+RB+Start -> Back always take precedence over LB+Start -> L3.
        _virtualKeys = [];
        foreach (var vk in _mapping.VirtualKeysItems)
        {
            if (vk is null || vk.DestinationItem is null || vk.SourceKeys is null || vk.SourceKeys.Count == 0)
            {
                continue;
            }

            var mask = XInputButtons.None;
            foreach (var src in vk.SourceKeys)
            {
                if (src.HasValue)
                {
                    mask |= src.Value;
                }
            }

            if (mask != XInputButtons.None)
            {
                _virtualKeys.Add(new CompiledVirtualKey(
                    mask,
                    vk.DestinationItem.Value,
                    BitOperations.PopCount((uint)mask)));
            }
        }

        _virtualKeys.Sort((a, b) => b.KeyCount.CompareTo(a.KeyCount));
    }

    public int MinimumRequiredReportLength => _minRequiredReportLength;

    public bool TryMapReport(
        ReadOnlySpan<byte> report,
        out GamepadState state,
        out string? diagnostic,
        MappingRuntimeOptions? runtimeOptions = null)
    {
        state = GamepadState.Neutral;
        diagnostic = null;

        if (report.IsEmpty)
        {
            diagnostic = "Received empty HID report.";
            return false;
        }

        if (_mapping.MappingItems.Count == 0 || _minRequiredReportLength == 0)
        {
            diagnostic = "Mapping contains no active MappingItems (empty .mfimapping file).";
            return false;
        }

        if (_mapping.ReportId.HasValue && report[0] != _mapping.ReportId.Value)
        {
            // Silently skip unrelated HID Report IDs (e.g., battery or status frames).
            return false;
        }

        if (report.Length < _minRequiredReportLength)
        {
            diagnostic = $"Skipped short HID report ({report.Length} bytes; mapping requires at least {_minRequiredReportLength} bytes).";
            return false;
        }

        var rawDeadzone = runtimeOptions?.Deadzone ?? _mapping.Deadzone ?? 0.06;
        var deadzone = double.IsFinite(rawDeadzone) ? Math.Clamp(rawDeadzone, 0.0, 0.5) : 0.06;

        var defaultThreshold = Math.Max(
            (byte)1,
            runtimeOptions?.ButtonThreshold ?? _mapping.ButtonThreshold ?? (byte)1);

        var invertLeftY = runtimeOptions?.InvertLeftY ?? false;
        var invertRightY = runtimeOptions?.InvertRightY ?? false;

        var buttons = XInputButtons.None;
        byte leftTrigger = 0;
        byte rightTrigger = 0;
        short lx = 0;
        short ly = 0;
        short rx = 0;
        short ry = 0;

        for (var i = 0; i < _mapping.MappingItems.Count; i++)
        {
            var item = _mapping.MappingItems[i];
            if (item is null || item.Type == GamepadMappingItemType.Empty)
            {
                continue;
            }

            var byteIndex = item.CustomIndex ?? i;
            if ((uint)byteIndex >= (uint)report.Length)
            {
                continue;
            }

            var raw = report[byteIndex];

            switch (item.Type)
            {
                case GamepadMappingItemType.Axis when item.AxisType.HasValue:
                    ApplyAxis(
                        raw,
                        item,
                        deadzone,
                        invertLeftY,
                        invertRightY,
                        ref lx,
                        ref ly,
                        ref rx,
                        ref ry,
                        ref leftTrigger,
                        ref rightTrigger);
                    break;

                case GamepadMappingItemType.Button or GamepadMappingItemType.DPad when item.ButtonType.HasValue:
                    if (IsButtonPressed(raw, item, defaultThreshold))
                    {
                        buttons |= item.ButtonType.Value;
                    }
                    break;
            }
        }

        // Apply virtual key combinations cleanly without accidental chaining:
        // rules are ordered most-specific first; matched physical bits are consumed
        // and synthesized outputs are accumulated separately.
        var remainingPhysical = buttons;
        var synthesized = XInputButtons.None;
        foreach (var vk in _virtualKeys)
        {
            if ((remainingPhysical & vk.SourceMask) == vk.SourceMask)
            {
                remainingPhysical &= ~vk.SourceMask;
                synthesized |= vk.Destination;
            }
        }

        buttons = remainingPhysical | synthesized;

        state = new GamepadState(buttons, leftTrigger, rightTrigger, lx, ly, rx, ry);
        return true;
    }

    private static bool IsButtonPressed(byte raw, GamepadMappingItem item, byte defaultThreshold)
    {
        if (item.CustomValue.HasValue && item.CustomValue.Value > 0)
        {
            var mask = item.CustomValue.Value;
            return (raw & mask) == mask;
        }

        var threshold = item.Threshold.HasValue ? Math.Max((byte)1, item.Threshold.Value) : defaultThreshold;
        return raw >= threshold;
    }

    private static void ApplyAxis(
        byte raw,
        GamepadMappingItem item,
        double deadzone,
        bool runtimeInvertLeftY,
        bool runtimeInvertRightY,
        ref short lx,
        ref short ly,
        ref short rx,
        ref short ry,
        ref byte leftTrigger,
        ref byte rightTrigger)
    {
        var axisType = item.AxisType!.Value;
        var itemInvert = item.InvertAxis ?? false;

        if (axisType is AxisType.LTrigger or AxisType.RTrigger)
        {
            var normalized = raw / 255.0;
            if (itemInvert)
            {
                normalized = 1.0 - normalized;
            }

            var triggerByte = (byte)Math.Clamp(Math.Round(normalized * 255.0), 0.0, 255.0);
            if (axisType == AxisType.LTrigger)
            {
                leftTrigger = triggerByte;
            }
            else
            {
                rightTrigger = triggerByte;
            }

            return;
        }

        // Thumbstick axis: normalize to [-1.0, +1.0].
        var stickNorm = NormalizeStickByte(raw, item.ConvertAxis ?? false, item.UnsignedCentered ?? false);
        stickNorm = ApplyDeadzone(stickNorm, deadzone);

        var invert = axisType switch
        {
            AxisType.Ly => itemInvert ^ runtimeInvertLeftY,
            AxisType.Ry => itemInvert ^ runtimeInvertRightY,
            _ => itemInvert,
        };

        if (invert)
        {
            stickNorm = -stickNorm;
        }

        var scaled = ToInt16Stick(stickNorm);
        switch (axisType)
        {
            case AxisType.Lx: lx = scaled; break;
            case AxisType.Ly: ly = scaled; break;
            case AxisType.Rx: rx = scaled; break;
            case AxisType.Ry: ry = scaled; break;
        }
    }

    /// <summary>
    /// Normalizes a raw 8-bit stick value into [-1.0, +1.0].
    /// </summary>
    public static double NormalizeStickByte(byte raw, bool signedTwoComplement, bool unsignedCentered)
    {
        if (signedTwoComplement)
        {
            // SteelSeries Nimbus encoding: sbyte (0 = center, 1..127 = +, 128..255 = -128..-1).
            var signedVal = unchecked((sbyte)raw);
            return signedVal < 0
                ? signedVal / 128.0
                : signedVal / 127.0;
        }

        if (unsignedCentered)
        {
            // Standard unsigned HID joystick encoding: 0..255 with 128 = center.
            return raw < 128
                ? (raw - 128) / 128.0
                : (raw - 128) / 127.0;
        }

        // Unipolar 0..255 mapped to [0.0, 1.0] (fallback if ConvertAxis=false on a stick).
        return raw / 255.0;
    }

    /// <summary>
    /// Applies a smooth rescaled deadzone so stick output ramps continuously from 0 at the deadzone edge to 1 at full tilt.
    /// </summary>
    public static double ApplyDeadzone(double normalized, double deadzone)
    {
        if (!double.IsFinite(normalized))
        {
            return 0.0;
        }

        if (!double.IsFinite(deadzone) || deadzone <= 0.0)
        {
            return Math.Clamp(normalized, -1.0, 1.0);
        }

        var clampedDz = Math.Clamp(deadzone, 0.0, 0.99);
        var abs = Math.Abs(normalized);
        if (abs <= clampedDz)
        {
            return 0.0;
        }

        var rescaled = (abs - clampedDz) / (1.0 - clampedDz);
        return Math.Clamp(Math.Sign(normalized) * rescaled, -1.0, 1.0);
    }

    public static short ToInt16Stick(double normalized)
    {
        if (!double.IsFinite(normalized))
        {
            return 0;
        }

        var clamped = Math.Clamp(normalized, -1.0, 1.0);
        var value = clamped >= 0.0
            ? Math.Round(clamped * short.MaxValue)
            : Math.Round(clamped * 32768.0);

        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }
}
