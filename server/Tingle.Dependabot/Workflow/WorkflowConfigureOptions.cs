using Microsoft.Extensions.Options;

namespace Tingle.Dependabot.Workflow;

internal class WorkflowConfigureOptions : IValidateOptions<WorkflowOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkflowOptions options)
    {
        if (options.JobsApiUrl is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.JobsApiUrl)}' is required");
        }

        if (options.JobsPlatform is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.JobsPlatform)}' is required");
        }

        var platform = options.JobsPlatform.Value;
        if (platform is Models.Management.UpdateJobPlatform.ContainerApps)
        {
            if (string.IsNullOrWhiteSpace(options.ResourceGroupId))
            {
                return ValidateOptionsResult.Fail($"'{nameof(options.ResourceGroupId)}' cannot be null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(options.AppEnvironmentId))
            {
                return ValidateOptionsResult.Fail($"'{nameof(options.AppEnvironmentId)}' cannot be null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(options.LogAnalyticsWorkspaceId))
            {
                return ValidateOptionsResult.Fail($"'{nameof(options.LogAnalyticsWorkspaceId)}' cannot be null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(options.Location))
            {
                return ValidateOptionsResult.Fail($"'{nameof(options.Location)}' cannot be null or whitespace");
            }
        }

        if (platform is Models.Management.UpdateJobPlatform.DockerCompose)
        {
            if (string.IsNullOrWhiteSpace(options.DockerNetwork))
            {
                return ValidateOptionsResult.Fail($"'{nameof(options.DockerNetwork)}' cannot be null or whitespace");
            }
        }

        if (string.IsNullOrWhiteSpace(options.UpdaterImageTag))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.UpdaterImageTag)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.WorkingDirectory)}' cannot be null or whitespace");
        }

        return ValidateOptionsResult.Success;
    }
}
