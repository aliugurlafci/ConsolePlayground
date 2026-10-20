using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Xml;

namespace CSPLibraries.Sap.Workers;

internal static class SapJsonConverters
{
    internal static readonly JsonSerializerOptions DefaultOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        options.Converters.Add(new SapDateTimeConverter());
        options.Converters.Add(new SapDateTimeOffsetConverter());
        options.Converters.Add(new SapTimeSpanConverter());
#if !NET6_0_OR_GREATER
        options.Converters.Add(new DateOnlyJsonConverter());
        options.Converters.Add(new TimeOnlyJsonConverter());
#endif
        options.MakeReadOnly();
        return options;
    }

    private static bool TryParseODataDate(ReadOnlySpan<char> text, out DateTimeOffset value)
    {
        value = default;

        if (!text.StartsWith("/Date(", StringComparison.Ordinal) || !text.EndsWith(")/", StringComparison.Ordinal))
            return false;

        ReadOnlySpan<char> body = text[6..^2];
        if (body.IsEmpty) return false;

        int offsetMinutes = 0;

        int signIndex = body[1..].IndexOfAny('+', '-');
        if (signIndex >= 0)
        {
            signIndex++;
            if (!int.TryParse(body[(signIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out offsetMinutes))
                return false;
            if (body[signIndex] == '-') offsetMinutes = -offsetMinutes;
            body = body[..signIndex];
        }

        if (!long.TryParse(body, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long milliseconds))
            return false;

        value = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
        return true;
    }

    private sealed class SapDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("DateTime için string bekleniyordu.");

            if (reader.TryGetDateTime(out DateTime iso))
                return iso;

            string text = reader.GetString()!;
            if (TryParseODataDate(text, out DateTimeOffset odata))
                return odata.DateTime;

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
                return parsed;

            throw new JsonException("Geçersiz tarih değeri: " + text);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private sealed class SapDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("DateTimeOffset için string bekleniyordu.");

            if (reader.TryGetDateTimeOffset(out DateTimeOffset iso))
                return iso;

            string text = reader.GetString()!;
            if (TryParseODataDate(text, out DateTimeOffset odata))
                return odata;

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
                return parsed;

            throw new JsonException("Geçersiz tarih değeri: " + text);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private sealed class SapTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
                throw new JsonException("Boş süre değeri.");

            try
            {
                return text[0] is 'P' or '-'
                    ? XmlConvert.ToTimeSpan(text)
                    : TimeSpan.Parse(text, CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw new JsonException("Geçersiz süre değeri: " + text, ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            writer.WriteStringValue(XmlConvert.ToString(value));
    }
}
