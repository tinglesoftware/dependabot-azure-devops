using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Tingle.Dependabot.Workflow;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extensions on <see cref="IServiceCollection"/>.</summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Add <see cref="IDistributedLockProvider"/>, a provider for <see cref="IDistributedLock"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to be configured.</param>
    /// <param name="environment">The <see cref="IHostEnvironment"/> to use.</param>
    /// <param name="configuration">The root configuration instance from which to pull settings.</param>
    /// <returns></returns>
    public static IServiceCollection AddDistributedLockProvider(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        var configKey = ConfigurationPath.Combine("DistributedLocking", "FilePath");

        var path = configuration.GetValue<string?>(configKey);

        // when the path is null in development, set one
        if (string.IsNullOrWhiteSpace(path) && environment.IsDevelopment())
        {
            path = Path.Combine(environment.ContentRootPath, "distributed-locks");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"'{nameof(path)}' must be provided via configuration at '{configKey}'.");
        }

        services.AddSingleton<IDistributedLockProvider>(new FileDistributedSynchronizationProvider(new(path)));

        return services;
    }

    public static IServiceCollection AddWorkflowServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkflowOptions>(configuration);
        services.ConfigureOptions<WorkflowConfigureOptions>();

        services.AddSingleton<UpdateRunner>();
        services.AddSingleton<UpdateScheduler>();

        services.AddScoped<AzureDevOpsProvider>();
        services.AddScoped<Synchronizer>();

        return services;
    }
}
