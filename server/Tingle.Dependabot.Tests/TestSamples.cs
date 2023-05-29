namespace Tingle.Dependabot.Tests;

internal class TestSamples
{
    public static Stream GetResourceAsStream(string resourceName)
        => typeof(TestSamples).Assembly.GetManifestResourceStream($"{typeof(TestSamples).Namespace}.Samples.{resourceName}")!;

    public static Stream GetAzureDevOpsGitPush1() => GetResourceAsStream("git.push-1.json");
    public static Stream GetAzureDevOpsGitPush2() => GetResourceAsStream("git.push-2.json");
    public static Stream GetAzureDevOpsGitPush3() => GetResourceAsStream("git.push-3.json");
    public static Stream GetAzureDevOpsPullRequestUpdated1() => GetResourceAsStream("git.pullrequest.updated-1.json");
    public static Stream GetAzureDevOpsPullRequestUpdated2() => GetResourceAsStream("git.pullrequest.updated-2.json");
    public static Stream GetAzureDevOpsPullRequestMerged1() => GetResourceAsStream("git.pullrequest.merged-1.json");
    public static Stream GetAzureDevOpsPullRequestMerged2() => GetResourceAsStream("git.pullrequest.merged-2.json");
    public static Stream GetAzureDevOpsPullRequestCommentEvent1() => GetResourceAsStream("git-pullrequest-comment-event-1.json");
    public static Stream GetAzureDevOpsPullRequestCommentEvent2() => GetResourceAsStream("git-pullrequest-comment-event-2.json");

    public static Stream GetDependabot() => GetResourceAsStream("dependabot.yml");
    public static Stream GetSampleRegistries() => GetResourceAsStream("sample-registries.yml");
}
