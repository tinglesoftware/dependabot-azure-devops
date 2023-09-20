using System.ComponentModel;
using System.Globalization;

namespace Tingle.Dependabot.Workflow;

/// <summary>Easier manager and parser for URLs of projects on Azure DevOps.</summary>
[TypeConverter(typeof(AzureDevOpsProjectUrlTypeConverter))]
public readonly struct AzureDevOpsProjectUrl : IEquatable<AzureDevOpsProjectUrl>
{
    private readonly Uri uri; // helps with case slash matching as compared to a plain string

    public AzureDevOpsProjectUrl(string value) : this(new Uri(value)) { }

    public AzureDevOpsProjectUrl(Uri uri)
    {
        this.uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Scheme = uri.Scheme;
        var host = Hostname = uri.Host;
        Port = uri switch
        {
            { Scheme: "http", Port: 80 } => null,
            { Scheme: "https", Port: 443 } => null,
            _ => uri.Port,
        };

        var builder = new UriBuilder(uri) { UserName = null, Password = null };
        if (string.Equals(host, "dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            OrganizationName = uri.AbsolutePath.Split("/")[1];
            builder.Path = OrganizationName + "/";
            ProjectIdOrName = uri.AbsolutePath.Replace("_apis/projects/", "").Split("/")[2];
        }
        else if (host.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            OrganizationName = host.Split(".")[0];
            builder.Path = string.Empty;
            ProjectIdOrName = uri.AbsolutePath.Replace("_apis/projects/", "").Split("/")[1];
        }
        // TODO: add support for Azure DevOps Server here
        else throw new ArgumentException($"Error parsing: '{uri}' into components");

        OrganizationUrl = builder.Uri.ToString();
        UsesProjectId = Guid.TryParse(ProjectIdOrName, out _); // Azure uses GUID for identifiers
    }

    public static AzureDevOpsProjectUrl Create(string hostname, string organizationName, string projectIdOrName)
    {
        var builder = new UriBuilder(Uri.UriSchemeHttps, hostname);
        if (string.Equals(hostname, "dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = organizationName + "/" + projectIdOrName;
        }
        else if (hostname.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = "/" + projectIdOrName;
        }
        else throw new ArgumentException($"The hostname '{hostname}' cannot be used for creation.");

        return new(builder.Uri);
    }

    public string Scheme { get; }
    public string Hostname { get; }
    public int? Port { get; }
    public string OrganizationName { get; }
    public string OrganizationUrl { get; }
    public string ProjectIdOrName { get; }
    public bool UsesProjectId { get; }

    public string? ProjectId => UsesProjectId ? ProjectIdOrName : null;
    public string? ProjectName => UsesProjectId ? null : ProjectIdOrName;

    public string MakeRepositorySlug(string name) => $"{OrganizationName}/{ProjectName}/_git/{name}";

    public override string ToString() => uri.ToString();
    public override int GetHashCode() => uri.GetHashCode();
    public override bool Equals(object? obj) => obj is AzureDevOpsProjectUrl url && Equals(url);
    public bool Equals(AzureDevOpsProjectUrl other) => uri == other.uri;

    public static bool operator ==(AzureDevOpsProjectUrl left, AzureDevOpsProjectUrl right) => left.Equals(right);
    public static bool operator !=(AzureDevOpsProjectUrl left, AzureDevOpsProjectUrl right) => !(left == right);

    public static implicit operator AzureDevOpsProjectUrl(string value) => new(value);
    public static implicit operator AzureDevOpsProjectUrl(Uri value) => new(value);
    public static implicit operator string(AzureDevOpsProjectUrl url) => url.ToString();
    public static implicit operator Uri(AzureDevOpsProjectUrl url) => url.uri;

    private class AzureDevOpsProjectUrlTypeConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || sourceType == typeof(Uri);

        /// <inheritdoc/>
        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || destinationType == typeof(Uri);

        /// <inheritdoc/>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is Uri u) return new AzureDevOpsProjectUrl(u);
            else if (value is string s) return new AzureDevOpsProjectUrl(s);
            return base.ConvertFrom(context, culture, value);
        }

        /// <inheritdoc/>
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (value is AzureDevOpsProjectUrl u)
            {
                if (destinationType == typeof(Uri)) return u.uri;
                else if (destinationType == typeof(string)) return u.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
