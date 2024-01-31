using Tingle.Dependabot.Workflow;
using Xunit;

namespace Tingle.Dependabot.Tests.Workflow;

public class WorkflowOptionsTests
{
    [Fact]
    public void GetUpdaterImageTag_Works()
    {
        var project1 = new Dependabot.Models.Management.Project { UpdaterImageTag = "1.25" };
        var project2 = new Dependabot.Models.Management.Project { };
        var options = new WorkflowOptions
        {
            UpdaterImageTag = "1.26",
            UpdaterImageTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["nuget"] = "1.24",
            },
        };

        // ecosystem override
        Assert.Equal("1.24", options.GetUpdaterImageTag("nuget", project1));
        Assert.Equal("1.24", options.GetUpdaterImageTag("NUGET", project1));

        // project override
        Assert.Equal("1.25", options.GetUpdaterImageTag("npm", project1));

        // no overrides
        Assert.Equal("1.26", options.GetUpdaterImageTag("npm", project2));
    }
}
