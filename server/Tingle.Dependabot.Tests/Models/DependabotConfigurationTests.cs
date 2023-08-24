using Tingle.Dependabot.Models;
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
        Assert.Equal(2, configuration!.Version);
        Assert.NotNull(configuration.Updates!);
        Assert.Equal(2, configuration.Updates!.Count);

        var first = configuration.Updates[0];
        Assert.Equal("/", first.Directory);
        Assert.Equal("docker", first.PackageEcosystem);
        Assert.Equal(DependabotScheduleInterval.Weekly, first.Schedule?.Interval);
        Assert.Equal(new TimeOnly(3, 0), first.Schedule?.Time);
        Assert.Equal(DependabotScheduleDay.Sunday, first.Schedule?.Day);
        Assert.Null(first.InsecureExternalCodeExecution);

        var second = configuration.Updates[1];
        Assert.Equal("/client", second.Directory);
        Assert.Equal("npm", second.PackageEcosystem);
        Assert.Equal(DependabotScheduleInterval.Daily, second.Schedule?.Interval);
        Assert.Equal(new TimeOnly(3, 15), second.Schedule?.Time);
        Assert.Equal(DependabotScheduleDay.Monday, second.Schedule?.Day);
        Assert.Equal("deny", second.InsecureExternalCodeExecution);
    }
}
