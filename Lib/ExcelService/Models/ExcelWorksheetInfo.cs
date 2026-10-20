namespace CSPLibraries.Excel.Models;

public sealed record ExcelWorksheetInfo(int Index, string Name, int RowCount, int ColumnCount, string VisibleState)
{
    public bool IsVisible => string.Equals(VisibleState, "visible", StringComparison.OrdinalIgnoreCase);
}
