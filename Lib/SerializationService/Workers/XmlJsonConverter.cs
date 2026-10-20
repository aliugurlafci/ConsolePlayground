using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace CSPLibraries.Serialization.Workers;

internal static class XmlJsonConverter
{
    private const string AttributePrefix = "@";
    private const string TextKey = "#text";

    private static readonly XmlReaderSettings SafeSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true
    };

    internal static JsonObject XmlToJson(TextReader xml)
    {
        using XmlReader reader = XmlReader.Create(xml, SafeSettings);
        XElement root = XElement.Load(reader);
        return new JsonObject { [root.Name.LocalName] = Convert(root) };
    }

    internal static XDocument JsonToXml(JsonNode json, string rootName)
    {
        var root = new XElement(XmlConvert.EncodeLocalName(rootName));
        Fill(root, json);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static JsonNode? Convert(XElement element)
    {
        bool hasAttributes = element.HasAttributes;
        if (!element.HasElements && !hasAttributes)
            return element.IsEmpty ? null : JsonValue.Create(element.Value);

        var result = new JsonObject();
        foreach (XAttribute attribute in element.Attributes())
        {
            if (!attribute.IsNamespaceDeclaration)
                result[AttributePrefix + attribute.Name.LocalName] = attribute.Value;
        }

        foreach (IGrouping<string, XElement> group in element.Elements().GroupBy(e => e.Name.LocalName))
        {
            List<XElement> items = group.ToList();
            if (items.Count == 1)
            {
                result[group.Key] = Convert(items[0]);
                continue;
            }

            var array = new JsonArray();
            foreach (XElement item in items)
                array.Add(Convert(item));
            result[group.Key] = array;
        }

        if (!element.HasElements && !string.IsNullOrEmpty(element.Value))
            result[TextKey] = element.Value;

        return result;
    }

    private static void Fill(XElement target, JsonNode? node)
    {
        switch (node)
        {
            case null:
                return;

            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key.StartsWith(AttributePrefix, StringComparison.Ordinal))
                    {
                        target.SetAttributeValue(XmlConvert.EncodeLocalName(key[1..]), ToText(value));
                    }
                    else if (key == TextKey)
                    {
                        target.Add(new XText(ToText(value) ?? string.Empty));
                    }
                    else if (value is JsonArray array)
                    {
                        foreach (JsonNode? item in array)
                        {
                            var child = new XElement(XmlConvert.EncodeLocalName(key));
                            Fill(child, item);
                            target.Add(child);
                        }
                    }
                    else
                    {
                        var child = new XElement(XmlConvert.EncodeLocalName(key));
                        Fill(child, value);
                        target.Add(child);
                    }
                }

                return;

            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    var child = new XElement("item");
                    Fill(child, item);
                    target.Add(child);
                }

                return;

            default:
                target.Value = ToText(node) ?? string.Empty;
                return;
        }
    }

    private static string? ToText(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue(out string? text) => text,
        JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False => value.GetValue<bool>() ? "true" : "false",
        JsonValue value when value.GetValueKind() == JsonValueKind.Number => value.ToJsonString(),
        _ => node.ToJsonString()
    };
}
