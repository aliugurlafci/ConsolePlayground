using System.Text;
using CSPLibraries.Excel.Constants;
using CSPLibraries.Excel.Datas;
using CSPLibraries.Excel.Models;
using ExcelDataReader;
using ExcelDataReader.Exceptions;

namespace CSPLibraries.Excel.Workers;

internal static class ExcelReaderProvider
{
    static ExcelReaderProvider()
    {
#if NETCOREAPP
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
    }

    internal static IExcelDataReader Open(Stream stream, string? fileName, ExcelReadOptions options)
    {
        var configuration = new ExcelReaderConfiguration
        {
            Password = options.Password,
            LeaveOpen = true
        };

        if (options.CsvEncoding is not null)
            configuration.FallbackEncoding = options.CsvEncoding;

        try
        {
            return ResolveFormat(stream, fileName, options.Format) == ExcelFileFormat.Csv
                ? ExcelReaderFactory.CreateCsvReader(stream, configuration)
                : ExcelReaderFactory.CreateReader(stream, configuration);
        }
        catch (ExcelReaderException ex)
        {
            throw new ExcelServiceException($"Excel dosyası açılamadı ({fileName ?? "akış"}): {ex.Message}", ex);
        }
    }

    internal static void MoveToWorksheet(IExcelDataReader reader, int worksheetIndex)
    {
        if (worksheetIndex < 0 || worksheetIndex >= reader.ResultsCount)
            throw new ArgumentOutOfRangeException(nameof(worksheetIndex),
                $"Worksheet index {worksheetIndex} bulunamadı. Toplam worksheet sayısı: {reader.ResultsCount}");

        for (int i = 0; i < worksheetIndex; i++)
            reader.NextResult();
    }

    internal static void MoveToWorksheet(IExcelDataReader reader, string worksheetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);

        do
        {
            if (string.Equals(reader.Name, worksheetName, StringComparison.OrdinalIgnoreCase))
                return;
        }
        while (reader.NextResult());

        throw new ArgumentException($"'{worksheetName}' isimli worksheet bulunamadı.", nameof(worksheetName));
    }

    internal static List<string> GetWorksheetNames(IExcelDataReader reader)
    {
        var names = new List<string>(reader.ResultsCount);
        do
        {
            names.Add(reader.Name);
        }
        while (reader.NextResult());

        return names;
    }

    private static ExcelFileFormat ResolveFormat(Stream stream, string? fileName, ExcelFileFormat requested)
    {
        if (requested != ExcelFileFormat.Auto) return requested;

        string extension = Path.GetExtension(fileName) ?? string.Empty;
        if (ExcelConstants.CsvExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return ExcelFileFormat.Csv;
        if (ExcelConstants.ExcelExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return ExcelFileFormat.Excel;

        return HasExcelSignature(stream) ? ExcelFileFormat.Excel : ExcelFileFormat.Csv;
    }

    private static bool HasExcelSignature(Stream stream)
    {
        if (!stream.CanSeek) return true;

        long position = stream.Position;
        Span<byte> header = stackalloc byte[8];
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = position;

        ReadOnlySpan<byte> bytes = header[..read];
        return bytes.StartsWith(ExcelConstants.ZipSignature) || bytes.StartsWith(ExcelConstants.OleSignature);
    }
}
