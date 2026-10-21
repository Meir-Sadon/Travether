using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Travether.Api.Data;

/// <summary>Stores an enum as readable snake_case text: <c>CardRole.CoAdmin</c> → <c>"co_admin"</c>.</summary>
public sealed class SnakeCaseEnumConverter<TEnum>()
    : ValueConverter<TEnum, string>(v => EnumText.ToDb(v), v => EnumText.FromDb<TEnum>(v))
    where TEnum : struct, Enum;

public static class EnumText
{
    private static readonly ConcurrentDictionary<Enum, string> ToDbCache = new();

    public static string ToDb<TEnum>(TEnum value) where TEnum : struct, Enum =>
        ToDbCache.GetOrAdd(value, v => ToSnakeCase(v.ToString()));

    public static TEnum FromDb<TEnum>(string value) where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (ToDb(candidate) == value)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"'{value}' is not a valid {typeof(TEnum).Name}.");
    }

    /// <summary>All stored values, for CHECK constraints.</summary>
    public static IEnumerable<string> AllDbValues<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(ToDb);

    public static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
            }

            sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
