using System.ComponentModel.DataAnnotations;
using Tingle.Dependabot.Models.Dependabot;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tingle.Dependabot.Tests.Models;

public class DependabotConfigurationTests
{
    [Fact]
    public void Deserialization_Works()
    {
        using var stream = TestSamples.GetDependabot();
        using var reader = new StreamReader(stream);

        var deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance)
                                                    .IgnoreUnmatchedProperties()
                                                    .Build();

        var configuration = deserializer.Deserialize<DependabotConfiguration?>(reader);
        Assert.NotNull(configuration);
        Assert.Equal(2, configuration.Version);
        Assert.NotNull(configuration.Updates);
        Assert.Equal(2, configuration.Updates.Count);

        var first = configuration.Updates[0];
        Assert.Equal("/", first.Directory);
        Assert.Equal("docker", first.PackageEcosystem);
        Assert.Equal(DependabotScheduleInterval.Weekly, first.Schedule?.Interval);
        Assert.Equal(new TimeOnly(3, 0), first.Schedule?.Time);
        Assert.Equal(DependabotScheduleDay.Sunday, first.Schedule?.Day);
        Assert.Equal("Etc/UTC", first.Schedule?.Timezone);
        Assert.Null(first.InsecureExternalCodeExecution);
        Assert.Null(first.Registries);

        var second = configuration.Updates[1];
        Assert.Equal("/client", second.Directory);
        Assert.Equal("npm", second.PackageEcosystem);
        Assert.Equal(DependabotScheduleInterval.Daily, second.Schedule?.Interval);
        Assert.Equal(new TimeOnly(3, 15), second.Schedule?.Time);
        Assert.Equal(DependabotScheduleDay.Monday, second.Schedule?.Day);
        Assert.Equal("Etc/UTC", second.Schedule?.Timezone);
        Assert.Equal("deny", second.InsecureExternalCodeExecution);
        Assert.Equal(new[] { "reg1", "reg2", }, second.Registries);
    }

    [Fact]
    public void Validation_Works()
    {
        var configuration = new DependabotConfiguration
        {
            Version = 2,
            Updates = new List<DependabotUpdate>
            {
                new DependabotUpdate
                {
                    PackageEcosystem = "npm",
                    Directory = "/",
                    Registries = new List<string> { "dummy1", "dummy2", },
                },
            },
            Registries = new Dictionary<string, DependabotRegistry>
            {
                ["dummy1"] = new DependabotRegistry
                {
                    Type = "nuget",
                    Url = "https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json",
                    Token = "pwd_1234567890",
                },
                ["dummy2"] = new DependabotRegistry
                {
                    Type = "python-index",
                    Url = "https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example",
                    Username = "octocat@example.com",
                    Password = "pwd_1234567890",
                    ReplacesBase = true,
                },
            },
        };

        // works as expected
        var results = new List<ValidationResult>();
        var actual = RecursiveValidator.TryValidateObject(configuration, results);
        Assert.True(actual);
        Assert.Empty(results);

        // fails: registry not referenced
        configuration.Updates[0].Registries?.Clear();
        results = new List<ValidationResult>();
        actual = RecursiveValidator.TryValidateObject(configuration, results);
        Assert.False(actual);
        var val = Assert.Single(results);
        Assert.Empty(val.MemberNames);
        Assert.NotNull(val.ErrorMessage);
        Assert.Equal("Registries: 'dummy1,dummy2' have not been referenced by any update", val.ErrorMessage);

        // fails: registrynot configured
        configuration.Updates[0].Registries?.AddRange(new[] { "dummy1", "dummy2", "dummy3" });
        results = new List<ValidationResult>();
        actual = RecursiveValidator.TryValidateObject(configuration, results);
        Assert.False(actual);
        val = Assert.Single(results);
        Assert.Empty(val.MemberNames);
        Assert.NotNull(val.ErrorMessage);
        Assert.Equal("Referenced registries: 'dummy3' have not been configured in the root of dependabot.yml", val.ErrorMessage);
    }
}
