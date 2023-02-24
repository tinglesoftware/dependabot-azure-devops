using System.Runtime.Serialization;

namespace System;

internal static class SystemExtensions
{
    /// <summary>
    /// Adds an element with the provided key and value,
    /// provided the value is not equal to the type's default value (or empty for strings).
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to use</param>
    /// <param name="key">The object to use as the key of the element to add.</param>
    /// <param name="value">The object to use as the value of the element to add.</param>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    /// <exception cref="NotSupportedException">The dictionary is read-only.</exception>
    /// <returns></returns>
    public static IDictionary<TKey, TValue> AddIfNotDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue? value)
        where TKey : notnull
    {
        if (value is not null || value is string s && !string.IsNullOrWhiteSpace(s))
        {
            dictionary[key] = value;
        }

        return dictionary;
    }

    /// <summary>Gets the value declared on the member using <see cref="EnumMemberAttribute"/> or the default.</summary>
    /// <typeparam name="T">The <see cref="Type"/> of the enum.</typeparam>
    /// <param name="value">The value of the enum member/field.</param>
    public static string GetEnumMemberAttrValueOrDefault<T>(this T value) where T : struct, Enum
    {
        var type = typeof(T);
        if (!type.IsEnum) throw new InvalidOperationException("Only enum types are allowed.");

        var mi = type.GetMember(value.ToString()!);
        var attr = mi.FirstOrDefault()?.GetCustomAttributes(false)
                     .OfType<EnumMemberAttribute>()
                     .FirstOrDefault();

        return attr?.Value ?? value.ToString()!.ToLowerInvariant();
    }
}
