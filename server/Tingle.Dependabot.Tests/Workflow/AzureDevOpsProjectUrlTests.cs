using System.ComponentModel;
using Tingle.Dependabot.Workflow;
using Xunit;

namespace Tingle.Dependabot.Tests.Workflow;

public class AzureDevOpsProjectUrlTests
{
    [Theory]
    [InlineData("https://dev.azure.com/dependabot/Core", "dev.azure.com", "dependabot", "https://dev.azure.com/dependabot/", "Core", false)]
    [InlineData("https://dev.azure.com/dependabot/_apis/projects/Core", "dev.azure.com", "dependabot", "https://dev.azure.com/dependabot/", "Core", false)]
    [InlineData("https://dev.azure.com/dependabot/_apis/projects/cea8cb01-dd13-4588-b27a-55fa170e4e94", "dev.azure.com", "dependabot", "https://dev.azure.com/dependabot/", "cea8cb01-dd13-4588-b27a-55fa170e4e94", true)]
    [InlineData("https://dependabot.visualstudio.com/Core", "dependabot.visualstudio.com", "dependabot", "https://dependabot.visualstudio.com/", "Core", false)]
    public void Creation_WithParsing_Works(string projectUrl, string hostname, string organizationName, string organizationUrl, string projectIdOrName, bool usesProjectId)
    {
        var url = (AzureDevOpsProjectUrl)projectUrl;
        Assert.Equal(hostname, url.Hostname);
        Assert.Equal(organizationName, url.OrganizationName);
        Assert.Equal(organizationUrl, url.OrganizationUrl);
        Assert.Equal(projectIdOrName, url.ProjectIdOrName);
        if (usesProjectId)
        {
            Assert.NotNull(url.ProjectId);
            Assert.Null(url.ProjectName);
        }
        else
        {
            Assert.Null(url.ProjectId);
            Assert.NotNull(url.ProjectName);
        }
    }

    [Theory]
    [InlineData("https://dev.azure.com/dependabot/Core/", "dependabot-sample", "dependabot/Core/_git/dependabot-sample")]
    [InlineData("https://dependabot.visualstudio.com/Core", "dependabot-sample", "dependabot/Core/_git/dependabot-sample")]
    public void MakeRepositorySlug_Works_For_Azure(string projectUrl, string repoName, string expected)
    {
        var url = (AzureDevOpsProjectUrl)projectUrl;
        var actual = url.MakeRepositorySlug(repoName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConvertsToUriOrString()
    {
        var converter = TypeDescriptor.GetConverter(typeof(AzureDevOpsProjectUrl));
        Assert.NotNull(converter);
        var url = new AzureDevOpsProjectUrl("https://dependabot.visualstudio.com/Core");

        var actual = converter.ConvertTo(url, typeof(string));
        Assert.Equal(actual, "https://dependabot.visualstudio.com/Core");

        actual = converter.ConvertTo(url, typeof(Uri));
        Assert.Equal(actual, new Uri("https://dependabot.visualstudio.com/Core"));

        actual = converter.ConvertToString(url);
        Assert.Equal("https://dependabot.visualstudio.com/Core", actual);
    }

    [Fact]
    public void ConvertsFromUriOrString()
    {
        var expected = new AzureDevOpsProjectUrl("https://dependabot.visualstudio.com/Core");
        var converter = TypeDescriptor.GetConverter(typeof(AzureDevOpsProjectUrl));
        Assert.NotNull(converter);

        var actual = Assert.IsType<AzureDevOpsProjectUrl>(converter.ConvertFrom(null, null, new Uri("https://dependabot.visualstudio.com/Core")));
        Assert.Equal(expected, actual);

        actual = Assert.IsType<AzureDevOpsProjectUrl>(converter.ConvertFrom(null, null, "https://dependabot.visualstudio.com/Core"));
        Assert.Equal(expected, actual);

        actual = Assert.IsType<AzureDevOpsProjectUrl>(converter.ConvertFromString("https://dependabot.visualstudio.com/Core"));
        Assert.Equal(expected, actual);
    }
}
