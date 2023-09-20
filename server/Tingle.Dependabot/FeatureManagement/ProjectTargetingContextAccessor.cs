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

        // Build targeting context based off project info
        var projectId = httpContext.GetProjectId();
        var targetingContext = new TargetingContext { UserId = projectId, };

        return new ValueTask<TargetingContext>(targetingContext);
    }
}
