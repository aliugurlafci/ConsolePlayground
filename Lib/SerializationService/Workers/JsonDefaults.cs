using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CSPLibraries.Serialization.Workers;

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = Create(indented: false);

    internal static readonly JsonSerializerOptions IndentedOptions = Create(indented: true);

    internal static JsonSerializerOptions Create(bool indented, JsonSerializerOptions? source = null)
    {
        JsonSerializerOptions options = source is null
            ? new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            }
            : new JsonSerializerOptions(source);

        options.WriteIndented = indented;
        options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
#if !NET6_0_OR_GREATER
        if (source is null)
        {
            options.Converters.Add(new DateOnlyJsonConverter());
            options.Converters.Add(new TimeOnlyJsonConverter());
        }
#endif
        options.MakeReadOnly();
        return options;
    }
}
