using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Runtime.Serialization;
using System.Text.Json;

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

    /// <summary>
    /// Attach conversion of property to/from JSON stored in the database as a string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyBuilder">The <see cref="PropertyBuilder{TProperty}"/> to extend.</param>
    /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> to use.</param>
    /// <returns></returns>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> propertyBuilder, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

#pragma warning disable CS8603 // Possible null reference return.
        var converter = new ValueConverter<T, string?>(
            convertToProviderExpression: v => ConvertToJson(v, serializerOptions),
            convertFromProviderExpression: v => ConvertFromJson<T>(v, serializerOptions));

        var comparer = new ValueComparer<T>(
            equalsExpression: (l, r) => ConvertToJson(l, serializerOptions) == ConvertToJson(r, serializerOptions),
            hashCodeExpression: v => v == null ? 0 : ConvertToJson(v, serializerOptions).GetHashCode(),
            snapshotExpression: v => ConvertFromJson<T>(ConvertToJson(v, serializerOptions), serializerOptions));
#pragma warning restore CS8603 // Possible null reference return.

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueConverter(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);

        return propertyBuilder;
    }

    private static string ConvertToJson<T>(T value, JsonSerializerOptions? serializerOptions) => JsonSerializer.Serialize(value, serializerOptions);
    private static T? ConvertFromJson<T>(string? value, JsonSerializerOptions? serializerOptions) => value is null ? default : JsonSerializer.Deserialize<T>(value, serializerOptions);
}
