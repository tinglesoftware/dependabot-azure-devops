using Microsoft.FeatureManagement.FeatureFilters;

namespace Tingle.Dependabot.FeatureManagement;

/// <summary>
/// An implementation of <see cref="ITargetingContextAccessor"/> 
/// that creates a <see cref="TargetingContext"/> using the current <see cref="HttpContext"/>.
/// </summary>
internal class ProjectTargetingContextAccessor : ITargetingContextAccessor
{
    private readonly IHttpContextAccessor contextAccessor;

    public ProjectTargetingContextAccessor(IHttpContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }

    /// <inheritdoc/>
    public ValueTask<TargetingContext> GetContextAsync()
    {
        var httpContext = contextAccessor.HttpContext!;

        // Prepare the groups
        var groups = new List<string>(); // where can we get the workspace groups?

        // Build targeting context based off workspace info
        var workspaceId = httpContext.GetProjectId();
        var targetingContext = new TargetingContext
        {
            UserId = workspaceId,
            Groups = groups
        };

        return new ValueTask<TargetingContext>(targetingContext);
    }
}
