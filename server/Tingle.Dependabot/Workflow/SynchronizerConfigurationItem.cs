using System.Diagnostics.CodeAnalysis;

namespace Tingle.Dependabot.Workflow;

public readonly record struct SynchronizerConfigurationItem(string Id, string Name, string Slug, string? CommitId, string? Content)
{
    public SynchronizerConfigurationItem(string slug,
                                         Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repo,
                                         Microsoft.TeamFoundation.SourceControl.WebApi.GitItem? item)
        : this(Id: repo.Id.ToString(),
               Name: repo.Name,
               Slug: slug,
               CommitId: item?.LatestProcessedChange.CommitId,
               Content: item?.Content)
    { }

    [MemberNotNullWhen(true, nameof(CommitId))]
    [MemberNotNullWhen(true, nameof(Content))]
    public bool HasConfiguration => !string.IsNullOrEmpty(CommitId) && !string.IsNullOrEmpty(Content);
}
