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
}
