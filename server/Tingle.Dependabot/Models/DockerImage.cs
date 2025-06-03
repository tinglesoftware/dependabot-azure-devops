using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models;

/// <summary>Easier parser for docker images with tags or digest.</summary>
[JsonConverter(typeof(DockerImageJsonConverter))]
[TypeConverter(typeof(DockerImageTypeConverter))]
public readonly struct DockerImage(string repository, string? tag, string? digest) : IEquatable<DockerImage>
{
    public string Repository { get; } = repository;
    public string? Tag { get; } = tag;
    public string? Digest { get; } = digest;

    public override string ToString() => Digest is not null ? $"{Repository}@{Digest}" : $"{Repository}:{Tag}";
    public override int GetHashCode() => HashCode.Combine(Repository, Tag, Digest);
    public override bool Equals(object? obj) => obj is DockerImage url && Equals(url);
    public bool Equals(DockerImage other) => Repository == other.Repository && Tag == other.Tag && Digest == other.Digest;

    public static bool operator ==(DockerImage left, DockerImage right) => left.Equals(right);
    public static bool operator !=(DockerImage left, DockerImage right) => !(left == right);

    public static implicit operator DockerImage(string value) => Parse(value);
    public static implicit operator string(DockerImage image) => image.ToString();

    public static DockerImage Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string repository;
        string? tag, digest;
        if (value.Contains('@')) // this uses a digest
        {
            var parts = value.Split('@');
            repository = parts[0];
            tag = null;
            digest = parts[1];
        }
        else if (value.Contains(':')) // this uses a tag
        {
            var parts = value.Split(':');
            repository = parts[0];
            tag = parts[1];
            digest = null;
        }
        else // this is just the repository, assume latest tag
        {
            repository = value;
            tag = "latest";
            digest = null;
        }
        return new DockerImage(repository, tag, digest);
    }

    internal class DockerImageTypeConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || sourceType == typeof(Uri);

        /// <inheritdoc/>
        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || destinationType == typeof(Uri);

        /// <inheritdoc/>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string s) return Parse(s);
            return base.ConvertFrom(context, culture, value);
        }

        /// <inheritdoc/>
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (value is DockerImage i)
            {
                if (destinationType == typeof(string)) return i.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    internal class DockerImageJsonConverter : JsonConverter<DockerImage>
    {
        public override DockerImage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default;
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidOperationException("Only strings are supported");
            }

            var str = reader.GetString();
            return Parse(str!);
        }

        public override void Write(Utf8JsonWriter writer, DockerImage value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
