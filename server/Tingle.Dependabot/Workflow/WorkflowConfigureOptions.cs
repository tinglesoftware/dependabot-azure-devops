using Microsoft.Extensions.Options;

namespace Tingle.Dependabot.Workflow;

internal class WorkflowConfigureOptions : IPostConfigureOptions<WorkflowOptions>, IValidateOptions<WorkflowOptions>
{
    public void PostConfigure(string? name, WorkflowOptions options)
    {
        if (!options.IsInContainer)
        {
            if (!string.IsNullOrWhiteSpace(options.CertsDirectory))
            {
                if (!Path.IsPathRooted(options.CertsDirectory))
                {
                    options.CertsDirectory = Path.Combine(Directory.GetCurrentDirectory(), options.CertsDirectory);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.ProxyDirectory))
            {
                if (!Path.IsPathRooted(options.ProxyDirectory))
                {
                    options.ProxyDirectory = Path.Combine(Directory.GetCurrentDirectory(), options.ProxyDirectory);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.JobsDirectory))
            {
                if (!Path.IsPathRooted(options.JobsDirectory))
                {
                    options.JobsDirectory = Path.Combine(Directory.GetCurrentDirectory(), options.JobsDirectory);
                }
            }
        }
    }

    public ValidateOptionsResult Validate(string? name, WorkflowOptions options)
    {
        if (options.JobsApiUrl is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.JobsApiUrl)}' is required");
        }

        if (string.IsNullOrWhiteSpace(options.ProxyImageTag))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ProxyImageTag)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.UpdaterImageTag))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.UpdaterImageTag)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.ProxyDirectory))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ProxyDirectory)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.JobsDirectory))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.JobsDirectory)}' cannot be null or whitespace");
        }

        return ValidateOptionsResult.Success;
    }
}
