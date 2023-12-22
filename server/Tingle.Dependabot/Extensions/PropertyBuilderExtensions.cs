using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Extensions for <see cref="PropertyBuilder{TProperty}"/>.
/// </summary>
public static class PropertyBuilderExtensions
{
    /// <summary>
    /// Attach conversion of property to/from <see cref="T:List{T}"/> stored in the database as concatenated string of strings.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyBuilder">The <see cref="PropertyBuilder{TProperty}"/> to extend.</param>
    /// <param name="separator">The separator to use.</param>
    /// <returns></returns>
    public static PropertyBuilder<List<T>> HasArrayConversion<T>(this PropertyBuilder<List<T>> propertyBuilder, string separator = ",") where T : IConvertible
        => propertyBuilder.HasArrayConversion(separator: separator, serializerOptions: null);

    /// <summary>
    /// Attach conversion of property to/from <see cref="T:List{T}"/> stored in the database as concatenated string of strings.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyBuilder">The <see cref="PropertyBuilder{TProperty}"/> to extend.</param>
    /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> to use for enums.</param>
    /// <returns></returns>
    public static PropertyBuilder<List<T>> HasArrayConversion<T>(this PropertyBuilder<List<T>> propertyBuilder, JsonSerializerOptions? serializerOptions = null) where T : IConvertible
        => propertyBuilder.HasArrayConversion(separator: ",", serializerOptions: serializerOptions);

    /// <summary>
    /// Attach conversion of property to/from <see cref="T:List{T}"/> stored in the database as concatenated string of strings.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyBuilder">The <see cref="PropertyBuilder{TProperty}"/> to extend.</param>
    /// <param name="separator">The separator to use.</param>
    /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> to use for enums.</param>
    /// <returns></returns>
    public static PropertyBuilder<List<T>> HasArrayConversion<T>(this PropertyBuilder<List<T>> propertyBuilder, string separator, JsonSerializerOptions? serializerOptions)
        where T : IConvertible
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        var converter = new ValueConverter<List<T>, string?>(
            convertToProviderExpression: v => ConvertToString(v, separator, serializerOptions),
            convertFromProviderExpression: v => ConvertFromString<T>(v, separator, serializerOptions));

        var comparer = new ValueComparer<List<T>>(
            equalsExpression: (l, r) => ConvertToString(l, separator, serializerOptions) == ConvertToString(r, separator, serializerOptions),
            hashCodeExpression: v => v == null ? 0 : ConvertToString(v, separator, serializerOptions).GetHashCode(),
            snapshotExpression: v => ConvertFromString<T>(ConvertToString(v, separator, serializerOptions), separator, serializerOptions));

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueConverter(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);

        return propertyBuilder;
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

    [return: NotNullIfNotNull(nameof(value))]
    private static string? ConvertToString<T>(List<T>? value, string separator, JsonSerializerOptions? serializerOptions) where T : IConvertible
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(separator))
        {
            throw new ArgumentException($"'{nameof(separator)}' cannot be null or whitespace.", nameof(separator));
        }

        return typeof(T).IsEnum
            ? string.Join(separator, value.Select(t => EnumToString(t, serializerOptions)))
            : string.Join(separator, value);
    }

    private static List<T> ConvertFromString<T>(string? value, string separator, JsonSerializerOptions? serializerOptions) where T : IConvertible
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        if (string.IsNullOrWhiteSpace(separator))
        {
            throw new ArgumentException($"'{nameof(separator)}' cannot be null or whitespace.", nameof(separator));
        }

        var split = value.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        return typeof(T).IsEnum
            ? split.Select(v => EnumFromString<T>(v, serializerOptions)).ToList()
            : split.Select(v => (T)Convert.ChangeType(v, typeof(T))).ToList();
    }


    private static T EnumFromString<T>(string value, JsonSerializerOptions? serializerOptions) where T : IConvertible
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{nameof(value)}' cannot be null or whitespace.", nameof(value));
        }

        return JsonSerializer.Deserialize<T>($"\"{value}\"", serializerOptions)!;
    }

    private static string EnumToString<T>(T value, JsonSerializerOptions? serializerOptions)
        => JsonSerializer.Serialize(value, serializerOptions).Trim('"');


    private static string ConvertToJson<T>(T value, JsonSerializerOptions? serializerOptions) => JsonSerializer.Serialize(value, serializerOptions);

    private static T? ConvertFromJson<T>(string? value, JsonSerializerOptions? serializerOptions) => value is null ? default : JsonSerializer.Deserialize<T>(value, serializerOptions);
}
