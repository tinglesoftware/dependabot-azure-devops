using Microsoft.Extensions.Options;

namespace Tingle.Dependabot.Workflow;

internal class WorkflowConfigureOptions : IPostConfigureOptions<WorkflowOptions>, IValidateOptions<WorkflowOptions>
{
    private readonly IConfiguration configuration;

    public WorkflowConfigureOptions(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void PostConfigure(string? name, WorkflowOptions options)
    {
        options.SubscriptionPassword ??= configuration.GetValue<string?>("Authentication:Schemes:ServiceHooks:Credentials:vsts");
    }

    public ValidateOptionsResult Validate(string? name, WorkflowOptions options)
    {
        if (options.WebhookEndpoint is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.WebhookEndpoint)}' is required");
        }

        if (options.ProjectUrl is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ProjectUrl)}' is required");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectToken))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ProjectToken)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.SubscriptionPassword))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.SubscriptionPassword)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.ResourceGroupId))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ResourceGroupId)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.LogAnalyticsWorkspaceId))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.LogAnalyticsWorkspaceId)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.LogAnalyticsWorkspaceKey))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.LogAnalyticsWorkspaceKey)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.UpdaterContainerImageTemplate))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.UpdaterContainerImageTemplate)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.ManagedIdentityId))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ManagedIdentityId)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.Location))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.Location)}' cannot be null or whitespace");
        }

        return ValidateOptionsResult.Success;
    }
}
