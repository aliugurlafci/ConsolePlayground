namespace CSPLibraries.Excel.Models;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ExcelColumnAttribute : Attribute
{
    public ExcelColumnAttribute() { }

    public ExcelColumnAttribute(string name)
    {
        Name = name;
    }

    public string? Name { get; }

    public int Index { get; init; } = -1;

    public bool Ignore { get; init; }

    public string? Format { get; init; }

    public double Width { get; init; }

    public int Order { get; init; } = int.MaxValue;
}
