using Tingle.Dependabot.Models;
using Xunit;

namespace Tingle.Dependabot.Tests.Models;

public class UpdateJobResourcesTests
{
    [Fact]
    public void FromEcosystem_Works()
    {
        var values = Enum.GetValues<DependabotPackageEcosystem>();
        Assert.All(values, ecosystem => UpdateJobResources.FromEcosystem(ecosystem));
    }

    [Theory]
    [InlineData(DependabotPackageEcosystem.Bundler, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Cargo, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Composer, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Docker, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Elixir, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Elm, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.GitSubmodule, 0.1, 0.2)]
    [InlineData(DependabotPackageEcosystem.GithubActions, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.GoModules, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Gradle, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Maven, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Mix, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Npm, 0.25, 1.0)]
    [InlineData(DependabotPackageEcosystem.NuGet, 0.25, 0.2)]
    [InlineData(DependabotPackageEcosystem.Pip, 0.25, 0.5)]
    [InlineData(DependabotPackageEcosystem.Terraform, 0.25, 1.0)]
    public void FromEcosystem_ExpectedValues(DependabotPackageEcosystem ecosystem, double expectedCpu, double expectedMemory)
    {
        var resources = UpdateJobResources.FromEcosystem(ecosystem);
        Assert.Equal(expectedCpu, resources.Cpu);
        Assert.Equal(expectedMemory, resources.Memory);
    }
}
