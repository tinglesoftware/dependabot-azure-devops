using System.Diagnostics.CodeAnalysis;
using Tingle.Dependabot.Models.Azure;

namespace Tingle.Dependabot.Workflow;

public readonly record struct SynchronizerConfigurationItem(string Id, string Name, string Slug, string? CommitId, string? Content)
{
    public SynchronizerConfigurationItem(string slug,
                                         AzdoRepository repo,
                                         AzdoRepositoryItem? item)
        : this(Id: repo.Id,
               Name: repo.Name,
               Slug: slug,
               CommitId: item?.LatestProcessedChange.CommitId,
               Content: item?.Content)
    { }

    [MemberNotNullWhen(true, nameof(CommitId))]
    [MemberNotNullWhen(true, nameof(Content))]
    public bool HasConfiguration => !string.IsNullOrEmpty(CommitId) && !string.IsNullOrEmpty(Content);
}
