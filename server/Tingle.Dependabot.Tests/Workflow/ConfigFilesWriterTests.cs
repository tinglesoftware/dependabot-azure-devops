using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Workflow;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tingle.Dependabot.Tests.Workflow;

public class ConfigFilesWriterTests
{
    private const string ProjectId = "prj_1234567890";

    [Fact]
    public void MakeCredentialsMetadata_Works()
    {
        using var stream = TestSamples.GetSampleRegistries();
        using var reader = new StreamReader(stream);

        var deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance)
                                                    .IgnoreUnmatchedProperties()
                                                    .Build();

        var configuration = deserializer.Deserialize<DependabotConfiguration?>(reader);
        Assert.NotNull(configuration);

        var secrets = new Dictionary<string, string>();
        var project = new Dependabot.Models.Management.Project
        {
            Id = ProjectId,
            Url = "https://dev.azure.com/dependabot/dependabot",
            Name = "dependabot",
            UserId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            Token = "token",
            ProviderId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            Password = "burp-bump",
            AutoApprove = new(),
            AutoComplete = new(),
            GithubToken = "github-token",
        };
        var credentials = ConfigFilesWriter.MakeCredentials(configuration.Registries.Values, secrets, project);
        Assert.Equal(13, credentials.Count);
        var metadatas = ConfigFilesWriter.MakeCredentialsMetadata(credentials);
        Assert.Equal(13, metadatas.Count);

        // git_source (main repo)
        var metadata = metadatas[0];
        Assert.Equal(["type", "host"], metadata.Keys);
        Assert.Equal(["git_source", "dev.azure.com"], metadata.Values);

        // git_source (GitHub)
        metadata = metadatas[1];
        Assert.Equal(["type", "host"], metadata.Keys);
        Assert.Equal(["git_source", "github.com"], metadata.Values);

        // composer-repository
        metadata = metadatas[2];
        Assert.Equal(["type", "host", "url"], metadata.Keys);
        Assert.Equal(["composer_repository", "repo.packagist.com", "https://repo.packagist.com/example-company/"], metadata.Values);

        // docker-registry
        metadata = metadatas[3];
        Assert.Equal(["type", "replaces-base", "registry"], metadata.Keys);
        Assert.Equal(["docker_registry", "true", "registry.hub.docker.com"], metadata.Values);

        // git
        metadata = metadatas[4];
        Assert.Equal(["type", "url"], metadata.Keys);
        Assert.Equal(["git", "https://github.com"], metadata.Values);

        // hex-organization
        metadata = metadatas[5];
        Assert.Equal(["type", "organization"], metadata.Keys);
        Assert.Equal(["hex_organization", "github"], metadata.Values);

        // hex-repository
        metadata = metadatas[6];
        Assert.Equal(["type", "repo", "public-key-fingerprint", "url"], metadata.Keys);
        Assert.Equal(["hex_repository", "private-repo", "pkf_1234567890", "https://private-repo.example.com"], metadata.Values);

        // maven-repository
        metadata = metadatas[7];
        Assert.Equal(["type", "replaces-base", "url"], metadata.Keys);
        Assert.Equal(["maven_repository", "true", "https://artifactory.example.com"], metadata.Values);

        // npm-registry
        metadata = metadatas[8];
        Assert.Equal(["type", "replaces-base", "registry"], metadata.Keys);
        Assert.Equal(["npm_registry", "true", "npm.pkg.github.com"], metadata.Values);

        // nuget-feed
        metadata = metadatas[9];
        Assert.Equal(["type", "url"], metadata.Keys);
        Assert.Equal(["nuget_feed", "https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json"], metadata.Values);

        // python-index
        metadata = metadatas[10];
        Assert.Equal(["type", "replaces-base", "index-url"], metadata.Keys);
        Assert.Equal(["python_index", "true", "https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example"], metadata.Values);

        // rubygems-server
        metadata = metadatas[11];
        Assert.Equal(["type", "url"], metadata.Keys);
        Assert.Equal(["rubygems_server", "https://rubygems.pkg.github.com/octocat/github_api"], metadata.Values);

        // terraform-registry
        metadata = metadatas[12];
        Assert.Equal(["type", "host"], metadata.Keys);
        Assert.Equal(["terraform_registry", "terraform.example.com"], metadata.Values);
    }

    [Fact]
    public void MakeCredentials_Works()
    {
        using var stream = TestSamples.GetSampleRegistries();
        using var reader = new StreamReader(stream);

        var deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance)
                                                    .IgnoreUnmatchedProperties()
                                                    .Build();

        var configuration = deserializer.Deserialize<DependabotConfiguration?>(reader);
        Assert.NotNull(configuration);

        var secrets = new Dictionary<string, string>();
        var project = new Dependabot.Models.Management.Project
        {
            Id = ProjectId,
            Url = "https://dev.azure.com/dependabot/dependabot",
            Name = "dependabot",
            Token = "token",
            UserId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            ProviderId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            Password = "burp-bump",
            AutoApprove = new(),
            AutoComplete = new(),
            GithubToken = "github-token",
        };
        var credentials = ConfigFilesWriter.MakeCredentials(configuration.Registries.Values, secrets, project);
        Assert.Equal(13, credentials.Count);

        // git_source (main repo)
        var credential = credentials[0];
        Assert.Equal("git_source", Assert.Contains("type", credential));
        Assert.Equal("dev.azure.com", Assert.Contains("host", credential));
        Assert.Equal("x-access-token", Assert.Contains("username", credential));
        Assert.Equal("token", Assert.Contains("password", credential));

        // git_source (GitHub)
        credential = credentials[1];
        Assert.Equal("git_source", Assert.Contains("type", credential));
        Assert.Equal("github.com", Assert.Contains("host", credential));
        Assert.Equal("x-access-token", Assert.Contains("username", credential));
        Assert.Equal("github-token", Assert.Contains("password", credential));

        // composer-repository
        credential = credentials[2];
        Assert.Equal("composer_repository", Assert.Contains("type", credential));
        Assert.Equal("https://repo.packagist.com/example-company/", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.Equal("repo.packagist.com", Assert.Contains("host", credential));
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("octocat", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.DoesNotContain("replaces-base", credential);

        // docker-registry
        credential = credentials[3];
        Assert.Equal("docker_registry", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.Equal("registry.hub.docker.com", Assert.Contains("registry", credential));
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("octocat", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.Equal("true", Assert.Contains("replaces-base", credential));

        // git
        credential = credentials[4];
        Assert.Equal("git", Assert.Contains("type", credential));
        Assert.Equal("https://github.com", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("x-access-token", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.DoesNotContain("replaces-base", credential);

        // hex-organization
        credential = credentials[5];
        Assert.Equal("hex_organization", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.Equal("key_1234567890", Assert.Contains("key", credential));
        Assert.DoesNotContain("token", credential);
        Assert.Equal("github", Assert.Contains("organization", credential));
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.DoesNotContain("username", credential);
        Assert.DoesNotContain("password", credential);
        Assert.DoesNotContain("replaces-base", credential);

        // hex-repository
        credential = credentials[6];
        Assert.Equal("hex_repository", Assert.Contains("type", credential));
        Assert.Equal("https://private-repo.example.com", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.Equal("private-repo", Assert.Contains("repo", credential));
        Assert.Equal("ak_1234567890", Assert.Contains("auth-key", credential));
        Assert.Equal("pkf_1234567890", Assert.Contains("public-key-fingerprint", credential));
        Assert.DoesNotContain("username", credential);
        Assert.DoesNotContain("password", credential);
        Assert.DoesNotContain("replaces-base", credential);

        // maven-repository
        credential = credentials[7];
        Assert.Equal("maven_repository", Assert.Contains("type", credential));
        Assert.Equal("https://artifactory.example.com", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("octocat", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.Equal("true", Assert.Contains("replaces-base", credential));

        // npm-registry
        credential = credentials[8];
        Assert.Equal("npm_registry", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.Equal("npm.pkg.github.com", Assert.Contains("registry", credential));
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.Equal("tkn_1234567890", Assert.Contains("token", credential));
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.DoesNotContain("username", credential);
        Assert.DoesNotContain("password", credential);
        Assert.Equal("true", Assert.Contains("replaces-base", credential));

        // nuget-feed
        credential = credentials[9];
        Assert.Equal("nuget_feed", Assert.Contains("type", credential));
        Assert.Equal("https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("octocat@example.com", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.DoesNotContain("replaces-base", credential);

        // python-index
        credential = credentials[10];
        Assert.Equal("python_index", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.DoesNotContain("registry", credential);
        Assert.Equal("https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example", Assert.Contains("index-url", credential));
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example", Assert.Contains("index-url", credential));
        Assert.Equal("octocat@example.com", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.Equal("true", Assert.Contains("replaces-base", credential));

        // rubygems-server
        credential = credentials[11];
        Assert.Equal("rubygems_server", Assert.Contains("type", credential));
        Assert.Equal("https://rubygems.pkg.github.com/octocat/github_api", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.Equal("tkn_1234567890", Assert.Contains("token", credential));
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.DoesNotContain("username", credential);
        Assert.DoesNotContain("password", credential);
        Assert.DoesNotContain("replaces-base", credential);

        // terraform-registry
        credential = credentials[12];
        Assert.Equal("terraform_registry", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("index-url", credential);
        Assert.Equal("terraform.example.com", Assert.Contains("host", credential));
        Assert.DoesNotContain("key", credential);
        Assert.Equal("tkn_1234567890", Assert.Contains("token", credential));
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.DoesNotContain("username", credential);
        Assert.DoesNotContain("password", credential);
        Assert.DoesNotContain("replaces-base", credential);
    }

    [Theory]
    [InlineData(":${{MY-p_aT}}", ":cake")]
    [InlineData(":${{ MY-p_aT }}", ":cake")]
    [InlineData(":${MY-p_aT}", ":${MY-p_aT}")]
    public void ConvertPlaceholder_Works(string input, string expected)
    {
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-p_at"] = "cake",
        };
        var actual = ConfigFilesWriter.ConvertPlaceholder(input, secrets);
        Assert.Equal(expected, actual);
    }
}
