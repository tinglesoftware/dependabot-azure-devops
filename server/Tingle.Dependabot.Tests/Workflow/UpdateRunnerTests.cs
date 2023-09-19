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
    public void MakeExtraCredentials_Works_1()
    {
        using var stream = TestSamples.GetSampleRegistries();
        using var reader = new StreamReader(stream);

        var deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance)
                                                    .IgnoreUnmatchedProperties()
                                                    .Build();

        var configuration = deserializer.Deserialize<DependabotConfiguration?>(reader);
        Assert.NotNull(configuration);
        var registries = UpdateRunner.MakeExtraCredentials(configuration.Registries.Values, new Dictionary<string, string>());
        Assert.Equal(11, registries.Count);

        // composer-repository
        var registry = registries[0];
        Assert.Equal("composer_repository", Assert.Contains("type", registry));
        Assert.Equal("https://repo.packagist.com/example-company/", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.Equal("octocat", Assert.Contains("username", registry));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", registry));
        Assert.DoesNotContain("replaces-base", registry);

        // docker-registry
        registry = registries[1];
        Assert.Equal("docker_registry", Assert.Contains("type", registry));
        Assert.DoesNotContain("url", registry);
        Assert.Equal("registry.hub.docker.com", Assert.Contains("registry", registry));
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.Equal("octocat", Assert.Contains("username", registry));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", registry));
        Assert.Equal("true", Assert.Contains("replaces-base", registry));

        // git
        registry = registries[2];
        Assert.Equal("git", Assert.Contains("type", registry));
        Assert.Equal("https://github.com", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.Equal("x-access-token", Assert.Contains("username", registry));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", registry));
        Assert.DoesNotContain("replaces-base", registry);

        // hex-organization
        registry = registries[3];
        Assert.Equal("hex_organization", Assert.Contains("type", registry));
        Assert.DoesNotContain("url", registry);
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.Equal("key_1234567890", Assert.Contains("key", registry));
        Assert.DoesNotContain("token", registry);
        Assert.Equal("github", Assert.Contains("organization", registry));
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.DoesNotContain("username", registry);
        Assert.DoesNotContain("password", registry);
        Assert.DoesNotContain("replaces-base", registry);

        // hex-repository
        registry = registries[4];
        Assert.Equal("hex_repository", Assert.Contains("type", registry));
        Assert.Equal("https://private-repo.example.com", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.Equal("private-repo", Assert.Contains("repo", registry));
        Assert.Equal("ak_1234567890", Assert.Contains("auth-key", registry));
        Assert.Equal("pkf_1234567890", Assert.Contains("public-key-fingerprint", registry));
        Assert.DoesNotContain("username", registry);
        Assert.DoesNotContain("password", registry);
        Assert.DoesNotContain("replaces-base", registry);

        // maven-repository
        registry = registries[5];
        Assert.Equal("maven_repository", Assert.Contains("type", registry));
        Assert.Equal("https://artifactory.example.com", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.Equal("octocat", Assert.Contains("username", registry));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", registry));
        Assert.Equal("true", Assert.Contains("replaces-base", registry));

        // npm-registry
        registry = registries[6];
        Assert.Equal("npm_registry", Assert.Contains("type", registry));
        Assert.DoesNotContain("url", registry);
        Assert.Equal("npm.pkg.github.com", Assert.Contains("registry", registry));
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.Equal("tkn_1234567890", Assert.Contains("token", registry));
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.DoesNotContain("username", registry);
        Assert.DoesNotContain("password", registry);
        Assert.Equal("true", Assert.Contains("replaces-base", registry));

        // nuget-feed
        registry = registries[7];
        Assert.Equal("nuget_feed", Assert.Contains("type", registry));
        Assert.Equal("https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.Equal("octocat@example.com", Assert.Contains("username", registry));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", registry));
        Assert.DoesNotContain("replaces-base", registry);

        // python-index
        registry = registries[8];
        Assert.Equal("python_index", Assert.Contains("type", registry));
        Assert.Equal("https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.DoesNotContain("token", registry);
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.Equal("octocat@example.com", Assert.Contains("username", registry));
        Assert.Equal("pwd_1234567890", Assert.Contains("password", registry));
        Assert.Equal("true", Assert.Contains("replaces-base", registry));

        // rubygems-server
        registry = registries[9];
        Assert.Equal("rubygems_server", Assert.Contains("type", registry));
        Assert.Equal("https://rubygems.pkg.github.com/octocat/github_api", Assert.Contains("url", registry));
        Assert.DoesNotContain("registry", registry);
        Assert.DoesNotContain("host", registry);
        Assert.DoesNotContain("key", registry);
        Assert.Equal("tkn_1234567890", Assert.Contains("token", registry));
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.DoesNotContain("username", registry);
        Assert.DoesNotContain("password", registry);
        Assert.DoesNotContain("replaces-base", registry);

        // terraform-registry
        registry = registries[10];
        Assert.Equal("terraform_registry", Assert.Contains("type", registry));
        Assert.DoesNotContain("url", registry);
        Assert.DoesNotContain("registry", registry);
        Assert.Equal("terraform.example.com", Assert.Contains("host", registry));
        Assert.DoesNotContain("key", registry);
        Assert.Equal("tkn_1234567890", Assert.Contains("token", registry));
        Assert.DoesNotContain("organization", registry);
        Assert.DoesNotContain("repo", registry);
        Assert.DoesNotContain("auth-key", registry);
        Assert.DoesNotContain("public-key-fingerprint", registry);
        Assert.DoesNotContain("username", registry);
        Assert.DoesNotContain("password", registry);
        Assert.DoesNotContain("replaces-base", registry);
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
}
