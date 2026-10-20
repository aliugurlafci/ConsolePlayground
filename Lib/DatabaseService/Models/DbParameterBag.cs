using System.Data;
using System.Data.Common;
using CSPLibraries.Database.Workers;
using Microsoft.Data.SqlClient;

namespace CSPLibraries.Database.Models;

public sealed class DbParameterBag
{
    private readonly List<ParameterSpec> _parameters = [];
    private readonly Dictionary<string, object?> _outputs = new(StringComparer.OrdinalIgnoreCase);

    public DbParameterBag Add(string name, object? value, DbType? dbType = null,
                              ParameterDirection direction = ParameterDirection.Input,
                              int? size = null, byte? precision = null, byte? scale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _parameters.Add(new ParameterSpec(name, value, dbType, direction, size, precision, scale, null));
        return this;
    }

    public DbParameterBag AddOutput(string name, DbType dbType, int? size = null) =>
        Add(name, null, dbType, ParameterDirection.Output, size);

    public DbParameterBag AddReturnValue(string name = "ReturnValue") =>
        Add(name, null, DbType.Int32, ParameterDirection.ReturnValue);

    public DbParameterBag AddTableValued(string name, DataTable table, string typeName)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        _parameters.Add(new ParameterSpec(name, table, null, ParameterDirection.Input, null, null, null, parameter =>
        {
            if (parameter is not SqlParameter sql)
                throw new NotSupportedException("Table-valued parametreler yalnızca SQL Server'da desteklenir.");

            sql.SqlDbType = SqlDbType.Structured;
            sql.TypeName = typeName;
        }));
        return this;
    }

    public DbParameterBag Add(string name, object? value, Action<DbParameter> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _parameters.Add(new ParameterSpec(name, value, null, ParameterDirection.Input, null, null, null, configure));
        return this;
    }

    public T? Get<T>(string name)
    {
        string key = name.TrimStart('@', ':', '?');
        if (!_outputs.TryGetValue(key, out object? value))
            throw new KeyNotFoundException($"'{name}' isimli output parametresi bulunamadı veya komut henüz çalıştırılmadı.");

        return value is null ? default : DbValueConverter.Convert<T>(value);
    }

    internal IReadOnlyList<ParameterSpec> Parameters => _parameters;

    internal void CaptureOutput(string name, object? value) => _outputs[name] = value is DBNull ? null : value;

    internal sealed record ParameterSpec(
        string Name, object? Value, DbType? DbType, ParameterDirection Direction,
        int? Size, byte? Precision, byte? Scale, Action<DbParameter>? Configure);
}
