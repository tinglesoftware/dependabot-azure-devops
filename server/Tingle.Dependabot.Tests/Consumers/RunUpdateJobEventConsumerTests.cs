using Tingle.Dependabot.Consumers;
using Xunit;

public class RunUpdateJobEventConsumerTests
{

    [Theory]
    [MemberData(nameof(ConvertEcosystemToPackageManagerValues))]
    public void ConvertEcosystemToPackageManager_Works(string ecosystem, string expected)
    {
        var actual = RunUpdateJobEventConsumer.ConvertEcosystemToPackageManager(ecosystem);
        Assert.Equal(expected, actual);
    }

    public static TheoryData<string, string> ConvertEcosystemToPackageManagerValues => new()
    {
        { "dotnet-sdk", "dotnet_sdk" },
        { "github-actions", "github_actions" },
        { "gitsubmodule", "submodules" },
        { "gomod", "go_modules" },
        { "mix", "hex" },
        { "npm", "npm_and_yarn" },
        { "yarn", "npm_and_yarn" },
        { "pnpm", "npm_and_yarn" },
        { "pipenv", "pip" },
        { "pip-compile", "pip" },
        { "poetry", "pip" },

        // retained
        { "nuget", "nuget" },
        { "gradle", "gradle" },
        { "maven", "maven" },
        { "swift", "swift" },
        { "devcontainers", "devcontainers" },
        { "terraform", "terraform" },
        { "docker", "docker" },
    };
}
