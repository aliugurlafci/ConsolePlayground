using System.Security;
using CSPLibraries.Excel.Constants;

namespace CSPLibraries.Excel.Workers;

internal sealed class XlsxStyleRegistry
{
    private readonly Dictionary<string, int> _styleByFormat = new(StringComparer.Ordinal);
    private readonly List<int> _numberFormatIds = [];
    private readonly List<(int Id, string Code)> _customFormats = [];

    internal int GetStyle(string? format)
    {
        if (string.IsNullOrEmpty(format) || format == "General") return 0;
        if (_styleByFormat.TryGetValue(format, out int style)) return style;

        if (!XlsxConstants.BuiltInNumberFormats.TryGetValue(format, out int numberFormatId))
        {
            numberFormatId = XlsxConstants.FirstCustomNumberFormatId + _customFormats.Count;
            _customFormats.Add((numberFormatId, format));
        }

        _numberFormatIds.Add(numberFormatId);
        style = XlsxConstants.HeaderStyleIndex + _numberFormatIds.Count;
        _styleByFormat[format] = style;
        return style;
    }

    internal void Write(TextWriter writer)
    {
        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write($"<styleSheet xmlns=\"{XlsxConstants.SpreadsheetNs}\">");

        if (_customFormats.Count > 0)
        {
            writer.Write($"<numFmts count=\"{_customFormats.Count}\">");
            foreach (var (id, code) in _customFormats)
                writer.Write($"<numFmt numFmtId=\"{id}\" formatCode=\"{SecurityElement.Escape(code)}\"/>");
            writer.Write("</numFmts>");
        }

        writer.Write("<fonts count=\"2\">" +
                     "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                     "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                     "</fonts>");
        writer.Write("<fills count=\"3\">" +
                     "<fill><patternFill patternType=\"none\"/></fill>" +
                     "<fill><patternFill patternType=\"gray125\"/></fill>" +
                     "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9E1F2\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                     "</fills>");
        writer.Write("<borders count=\"2\">" +
                     "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                     "<border><left/><right/><top/><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border>" +
                     "</borders>");
        writer.Write("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");

        writer.Write($"<cellXfs count=\"{2 + _numberFormatIds.Count}\">");
        writer.Write("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
        writer.Write("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
        foreach (int id in _numberFormatIds)
            writer.Write($"<xf numFmtId=\"{id}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
        writer.Write("</cellXfs>");

        writer.Write("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
        writer.Write("</styleSheet>");
    }
}
