using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CSPLibraries.Database.Constants;
using CSPLibraries.Database.Models;

namespace CSPLibraries.Database.Workers;

internal static class ParameterBinder
{
    internal static string Bind(DbCommand command, string sql, object? parameters, DatabaseContext context)
    {
        switch (parameters)
        {
            case null:
                return sql;

            case DbParameterBag bag:
                foreach (DbParameterBag.ParameterSpec spec in bag.Parameters)
                    AddSpec(command, spec, context);
                return sql;

            case DbParameter single:
                command.Parameters.Add(single);
                return sql;

            case IEnumerable<DbParameter> list:
                foreach (DbParameter parameter in list)
                    command.Parameters.Add(parameter);
                return sql;

            case IEnumerable<KeyValuePair<string, object?>> pairs:
                foreach (var (name, value) in pairs)
                    sql = AddValue(command, sql, name, value, context);
                return sql;

            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                    sql = AddValue(command, sql, entry.Key.ToString()!, entry.Value, context);
                return sql;
        }

        foreach (ParameterPlan plan in GetPlans(parameters.GetType(), context.ParameterPrefix))
        {
            object? value = plan.Getter(parameters);
            if (plan.MayBeList && value is IEnumerable items and not string and not byte[] and not DataTable)
            {
                sql = ExpandList(command, sql, plan.BareName, items, context);
                continue;
            }

            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = plan.ParameterName;
            SetValue(parameter, value);
            command.Parameters.Add(parameter);
        }

        return sql;
    }

    private static readonly ConcurrentDictionary<(Type Type, char Prefix), ParameterPlan[]> Plans = new();

    private static ParameterPlan[] GetPlans(Type type, char prefix) =>
        Plans.GetOrAdd((type, prefix), static key => key.Type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0)
            .Select(p => new ParameterPlan(
                key.Prefix == '@' ? "@" + p.Name : p.Name,
                p.Name,
                CompileGetter(key.Type, p),
                p.PropertyType == typeof(object) ||
                (typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string) &&
                 p.PropertyType != typeof(byte[]) && p.PropertyType != typeof(DataTable))))
            .ToArray());

    private static Func<object, object?> CompileGetter(Type type, PropertyInfo property)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        Expression value = Expression.Property(Expression.Convert(instance, type), property);

        Type underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (underlying.IsEnum && underlying == property.PropertyType)
            value = Expression.Convert(value, Enum.GetUnderlyingType(underlying));

        return Expression.Lambda<Func<object, object?>>(Expression.Convert(value, typeof(object)), instance).Compile();
    }

    private sealed record ParameterPlan(string ParameterName, string BareName, Func<object, object?> Getter, bool MayBeList);

    internal static void CaptureOutputs(DbCommand command, object? parameters)
    {
        if (parameters is not DbParameterBag bag) return;

        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.Direction != ParameterDirection.Input)
                bag.CaptureOutput(parameter.ParameterName.TrimStart('@', ':', '?'), parameter.Value);
        }
    }

    internal static string ParameterName(string name, DatabaseContext context)
    {
        string bare = name.TrimStart('@', ':', '?');
        return context.ParameterPrefix == '@' ? "@" + bare : bare;
    }

    private static string AddValue(DbCommand command, string sql, string name, object? value, DatabaseContext context)
    {
        if (value is IEnumerable items and not string and not byte[] and not DataTable)
            return ExpandList(command, sql, name, items, context);

        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = ParameterName(name, context);
        SetValue(parameter, value);
        command.Parameters.Add(parameter);
        return sql;
    }

    private static void AddSpec(DbCommand command, DbParameterBag.ParameterSpec spec, DatabaseContext context)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = ParameterName(spec.Name, context);
        parameter.Direction = spec.Direction;
        SetValue(parameter, spec.Value);

        if (spec.DbType.HasValue) parameter.DbType = spec.DbType.Value;
        if (spec.Size.HasValue) parameter.Size = spec.Size.Value;
        if (spec.Precision.HasValue) parameter.Precision = spec.Precision.Value;
        if (spec.Scale.HasValue) parameter.Scale = spec.Scale.Value;
        spec.Configure?.Invoke(parameter);

        command.Parameters.Add(parameter);
    }

    private static void SetValue(DbParameter parameter, object? value)
    {
        switch (value)
        {
            case null:
                parameter.Value = DBNull.Value;
                break;

            case string text:
                parameter.DbType = DbType.String;
                parameter.Size = text.Length <= DatabaseDefaults.StringParameterSize ? DatabaseDefaults.StringParameterSize : -1;
                parameter.Value = text;
                break;

            case Enum enumValue:
                parameter.Value = Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()));
                break;

            case char character:
                parameter.Value = character.ToString();
                break;

#if !NET6_0_OR_GREATER
            case DateOnly date:
                parameter.DbType = DbType.Date;
                parameter.Value = date.ToDateTime(TimeOnly.MinValue);
                break;

            case TimeOnly time:
                parameter.DbType = DbType.Time;
                parameter.Value = time.ToTimeSpan();
                break;
#endif

            default:
                parameter.Value = value;
                break;
        }
    }

    private static string ExpandList(DbCommand command, string sql, string name, IEnumerable items, DatabaseContext context)
    {
        string bare = name.TrimStart('@', ':', '?');
        var placeholders = new StringBuilder();
        int index = 0;

        foreach (object? item in items)
        {
            string itemName = bare + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (index++ > 0) placeholders.Append(',');
            placeholders.Append(context.ParameterPrefix).Append(itemName);

            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = ParameterName(itemName, context);
            SetValue(parameter, item);
            command.Parameters.Add(parameter);
        }

        string list = index == 0 ? "NULL" : placeholders.ToString();
        string token = Regex.Escape(context.ParameterPrefix + bare);

        string withParentheses = Regex.Replace(sql, @"\(\s*" + token + @"\s*\)", "(" + list + ")",
                                               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (withParentheses != sql)
            return withParentheses;

        return Regex.Replace(sql, token + @"(?![\w$#@])", "(" + list + ")",
                             RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
