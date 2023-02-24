using Tingle.Extensions.Processing;

namespace Tingle.Dependabot.Tests;

internal class TestSamples
{
    private const string FolderNameSamples = "Samples";

    private static Stream GetAsStream(string fileName)
        => EmbeddedResourceHelper.GetResourceAsStream<TestSamples>(FolderNameSamples, fileName)!;

    public static Stream GetAzureDevOpsGitPush1() => GetAsStream("git.push-1.json");
    public static Stream GetAzureDevOpsGitPush2() => GetAsStream("git.push-2.json");
    public static Stream GetAzureDevOpsGitPush3() => GetAsStream("git.push-3.json");
    public static Stream GetAzureDevOpsPullRequestUpdated1() => GetAsStream("git.pullrequest.updated-1.json");
    public static Stream GetAzureDevOpsPullRequestUpdated2() => GetAsStream("git.pullrequest.updated-2.json");
    public static Stream GetAzureDevOpsPullRequestMerged1() => GetAsStream("git.pullrequest.merged-1.json");
    public static Stream GetAzureDevOpsPullRequestMerged2() => GetAsStream("git.pullrequest.merged-2.json");
    public static Stream GetAzureDevOpsPullRequestCommentEvent1() => GetAsStream("git-pullrequest-comment-event-1.json");
    public static Stream GetAzureDevOpsPullRequestCommentEvent2() => GetAsStream("git-pullrequest-comment-event-2.json");

    public static Stream GetDependabot() => GetAsStream("dependabot.yml");
    public static Stream GetSampleRegistries() => GetAsStream("sample-registries.yml");
}
