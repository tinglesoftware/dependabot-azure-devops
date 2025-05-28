using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public record DependabotProxyConfig(
    [property: JsonPropertyName("all_credentials")] IReadOnlyList<DependabotCredential> Credentials,
    [property: JsonPropertyName("ca")] CertificateAuthority CA);

public class DependabotCredential : Dictionary<string, string>
{
    public DependabotCredential() { }
    public DependabotCredential(IEnumerable<KeyValuePair<string, string>> collection) : base(collection, null) { }
}

// CertificateAuthority includes the MITM CA certificate and private key
public record CertificateAuthority(
    [property: JsonPropertyName("cert")] string Cert,
    [property: JsonPropertyName("key")] string Key);
