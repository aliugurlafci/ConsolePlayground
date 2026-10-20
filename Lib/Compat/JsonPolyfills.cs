#if !NET6_0_OR_GREATER
using System.Globalization;

namespace System.Text.Json.Serialization;

internal sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("DateOnly için string bekleniyordu.");

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));

    public override DateOnly ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.ToString(Format, CultureInfo.InvariantCulture));

    private static DateOnly Parse(string? text) =>
        DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value)
            ? value
            : throw new JsonException("Geçersiz DateOnly değeri: " + text);
}

internal sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("TimeOnly için string bekleniyordu.");

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Format(value));

    public override TimeOnly ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WritePropertyName(Format(value));

    private static string Format(TimeOnly value) => value.ToTimeSpan().ToString("c", CultureInfo.InvariantCulture);

    private static TimeOnly Parse(string? text)
    {
        if (text is null || !TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan time) ||
            time < TimeSpan.Zero || time.Ticks >= TimeSpan.TicksPerDay)
        {
            throw new JsonException("Geçersiz TimeOnly değeri: " + text);
        }

        return TimeOnly.FromTimeSpan(time);
    }
}
#endif
