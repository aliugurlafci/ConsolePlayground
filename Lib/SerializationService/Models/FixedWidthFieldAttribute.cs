using CSPLibraries.Serialization.Datas;

namespace CSPLibraries.Serialization.Models;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class FixedWidthFieldAttribute(int start, int length) : Attribute
{
    public int Start { get; } = start;

    public int Length { get; } = length;

    public FieldAlignment Alignment { get; init; } = FieldAlignment.Auto;

    public char PadChar { get; init; } = ' ';

    public string? Format { get; init; }
}
