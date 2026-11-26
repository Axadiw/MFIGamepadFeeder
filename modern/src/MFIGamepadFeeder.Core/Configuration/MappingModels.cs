using System.Text.Json;
using System.Text.Json.Serialization;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Core.Configuration;

/// <summary>
/// Matches the exact numeric values stored in legacy .mfimapping files.
/// </summary>
public enum GamepadMappingItemType
{
    Axis = 0,
    Button = 1,
    DPad = 2,
    Empty = 3,
}

public sealed class GamepadMappingItem
{
    public GamepadMappingItemType Type { get; set; } = GamepadMappingItemType.Empty;
    public bool? InvertAxis { get; set; } = false;

    /// <summary>
    /// Legacy flag indicating signed 8-bit two's-complement stick encoding (0=center, 1..127=+, 128..255=-),
    /// as transmitted by the SteelSeries Nimbus.
    /// </summary>
    public bool? ConvertAxis { get; set; } = false;

    /// <summary>
    /// Optional flag for controllers transmitting unsigned centered 8-bit stick values (0..255 with 128=center).
    /// </summary>
    public bool? UnsignedCentered { get; set; }

    public XInputButtons? ButtonType { get; set; }
    public AxisType? AxisType { get; set; }

    /// <summary>
    /// Optional explicit byte index in the HID report (PR #73 compatibility for Nimbus+ bitmask channels).
    /// When null, the item's sequential index in MappingItems is used.
    /// </summary>
    public int? CustomIndex { get; set; }

    /// <summary>
    /// Optional bitmask for controllers that pack multiple buttons into one byte (e.g., Nimbus+).
    /// When set, the button is active iff (rawByte &amp; CustomValue) == CustomValue.
    /// </summary>
    public int? CustomValue { get; set; }

    /// <summary>
    /// Optional per-button pressure threshold (1..255) for analog-pressure MFi buttons.
    /// </summary>
    public byte? Threshold { get; set; }
}

public sealed class VirtualKeyMappingItem
{
    public List<XInputButtons?> SourceKeys { get; set; } = [];
    public XInputButtons? DestinationItem { get; set; }
}

public sealed class GamepadMapping
{
    public List<GamepadMappingItem> MappingItems { get; set; } = [];
    public List<VirtualKeyMappingItem> VirtualKeysItems { get; set; } = [];

    /// <summary>
    /// Optional expected HID Report ID (byte 0). When set, reports with a different Report ID
    /// (such as battery or status notifications) are cleanly ignored.
    /// </summary>
    public byte? ReportId { get; set; }

    /// <summary>
    /// Optional default stick deadzone in [0.0, 0.5].
    /// </summary>
    public double? Deadzone { get; set; }

    /// <summary>
    /// Optional default analog button activation threshold (1..255). Defaults to 1 (any non-zero byte).
    /// </summary>
    public byte? ButtonThreshold { get; set; }
}

public static class MappingSerializer
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new FlexibleEnumJsonConverterFactory() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static GamepadMapping Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Mapping JSON is empty.");
        }

        var mapping = JsonSerializer.Deserialize<GamepadMapping>(json, ReadOptions)
            ?? throw new InvalidDataException("Failed to deserialize mapping JSON.");

        mapping.MappingItems ??= [];
        mapping.VirtualKeysItems ??= [];
        return mapping;
    }

    public static GamepadMapping LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return Deserialize(json);
    }

    public static string Serialize(GamepadMapping mapping)
    {
        return JsonSerializer.Serialize(mapping, WriteOptions);
    }

    public static void SaveToFile(string path, GamepadMapping mapping)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, Serialize(mapping));
    }

    /// <summary>
    /// Accepts both legacy integer enum values (e.g. "ButtonType": 4096) and string names (e.g. "A").
    /// </summary>
    private sealed class FlexibleEnumJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(FlexibleEnumConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private sealed class FlexibleEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
        {
            public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    var raw = reader.GetInt64();
                    return (TEnum)Enum.ToObject(typeof(TEnum), raw);
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
                    {
                        return parsed;
                    }
                }

                throw new JsonException($"Invalid value for enum {typeof(TEnum).Name}.");
            }

            public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(Convert.ToInt64(value));
            }
        }
    }
}
