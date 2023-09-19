using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tingle.Dependabot.Tests.Workflow;

public class UpdateRunnerTests
{
    private readonly ITestOutputHelper outputHelper;

    public UpdateRunnerTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

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
        var credentials = UpdateRunner.MakeExtraCredentials(configuration.Registries.Values, new Dictionary<string, string>());
        Assert.Equal(11, credentials.Count);
        var metadatas = UpdateRunner.MakeCredentialsMetadata(credentials);
        Assert.Equal(11, metadatas.Count);

        // composer-repository
        var metadata = metadatas[0];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "composer_repository", "repo.packagist.com", }, metadata.Values);

        // docker-registry
        metadata = metadatas[1];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "docker_registry", "registry.hub.docker.com", }, metadata.Values);

        // git
        metadata = metadatas[2];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "git", "github.com", }, metadata.Values);

        // hex-organization
        metadata = metadatas[3];
        Assert.Equal(new[] { "type", }, metadata.Keys);
        Assert.Equal(new[] { "hex_organization", }, metadata.Values);

        // hex-repository
        metadata = metadatas[4];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "hex_repository", "private-repo.example.com", }, metadata.Values);

        // maven-repository
        metadata = metadatas[5];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "maven_repository", "artifactory.example.com", }, metadata.Values);

        // npm-registry
        metadata = metadatas[6];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "npm_registry", "npm.pkg.github.com", }, metadata.Values);

        // nuget-feed
        metadata = metadatas[7];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "nuget_feed", "pkgs.dev.azure.com", }, metadata.Values);

        // python-index
        metadata = metadatas[8];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "python_index", "pkgs.dev.azure.com", }, metadata.Values);

        // rubygems-server
        metadata = metadatas[9];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "rubygems_server", "rubygems.pkg.github.com", }, metadata.Values);

        // terraform-registry
        metadata = metadatas[10];
        Assert.Equal(new[] { "type", "host", }, metadata.Keys);
        Assert.Equal(new[] { "terraform_registry", "terraform.example.com", }, metadata.Values);
    }

    [Fact]
    public void MakeExtraCredentials_Works()
    {
        using var stream = TestSamples.GetSampleRegistries();
        using var reader = new StreamReader(stream);

        var deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance)
                                                    .IgnoreUnmatchedProperties()
                                                    .Build();

        var configuration = deserializer.Deserialize<DependabotConfiguration?>(reader);
        Assert.NotNull(configuration);
        var credentials = UpdateRunner.MakeExtraCredentials(configuration.Registries.Values, new Dictionary<string, string>());
        Assert.Equal(11, credentials.Count);

        // composer-repository
        var credential = credentials[0];
        Assert.Equal("composer_repository", Assert.Contains("type", credential));
        Assert.Equal("https://repo.packagist.com/example-company/", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("host", credential);
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
        credential = credentials[1];
        Assert.Equal("docker_registry", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.Equal("registry.hub.docker.com", Assert.Contains("registry", credential));
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
        credential = credentials[2];
        Assert.Equal("git", Assert.Contains("type", credential));
        Assert.Equal("https://github.com", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
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
        credential = credentials[3];
        Assert.Equal("hex_organization", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.DoesNotContain("registry", credential);
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
        credential = credentials[4];
        Assert.Equal("hex_repository", Assert.Contains("type", credential));
        Assert.Equal("https://private-repo.example.com", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
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
        credential = credentials[5];
        Assert.Equal("maven_repository", Assert.Contains("type", credential));
        Assert.Equal("https://artifactory.example.com", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
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
        credential = credentials[6];
        Assert.Equal("npm_registry", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.Equal("npm.pkg.github.com", Assert.Contains("registry", credential));
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
        credential = credentials[7];
        Assert.Equal("nuget_feed", Assert.Contains("type", credential));
        Assert.Equal("https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
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
        credential = credentials[8];
        Assert.Equal("python_index", Assert.Contains("type", credential));
        Assert.Equal("https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
        Assert.DoesNotContain("host", credential);
        Assert.DoesNotContain("key", credential);
        Assert.DoesNotContain("token", credential);
        Assert.DoesNotContain("organization", credential);
        Assert.DoesNotContain("repo", credential);
        Assert.DoesNotContain("auth-key", credential);
        Assert.DoesNotContain("public-key-fingerprint", credential);
        Assert.Equal("octocat@example.com", Assert.Contains("username", credential));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", credential));
        Assert.Equal("true", Assert.Contains("replaces-base", credential));

        // rubygems-server
        credential = credentials[9];
        Assert.Equal("rubygems_server", Assert.Contains("type", credential));
        Assert.Equal("https://rubygems.pkg.github.com/octocat/github_api", Assert.Contains("url", credential));
        Assert.DoesNotContain("registry", credential);
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
        credential = credentials[10];
        Assert.Equal("terraform_registry", Assert.Contains("type", credential));
        Assert.DoesNotContain("url", credential);
        Assert.DoesNotContain("registry", credential);
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

    [Fact]
    public void ConvertPlaceholder_Works()
    {
        var input = ":${{MY-p_aT}}";
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-p_at"] = "cake",
        };
        var result = UpdateRunner.ConvertPlaceholder(input, secrets);
        Assert.Equal(":cake", result);
    }

    [Theory]
    [MemberData(nameof(ConvertEcosystemToPackageManagerValues))]
    public void ConvertEcosystemToPackageManager_Works(string ecosystem, string expected)
    {
        var actual = UpdateRunner.ConvertEcosystemToPackageManager(ecosystem);
        Assert.Equal(expected, actual);
    }

    public static TheoryData<string, string> ConvertEcosystemToPackageManagerValues => new()
    {
        { "github-actions", "github_actions" },
        { "gitsubmodule", "submodules" },
        { "gomod", "go_modules" },
        { "mix", "hex" },
        { "npm", "npm_and_yarn" },
        { "yarn", "npm_and_yarn" },
        { "pipenv", "pip" },
        { "pip-compile", "pip" },
        { "poetry", "pip" },

        // retained
        { "nuget", "nuget" },
        { "npm", "npm_and_yarn" },
        { "gradle", "gradle" },
        { "maven", "maven" },
        { "swift", "swift" },
        { "terraform", "terraform" },
        { "docker", "docker" },
    };
}
