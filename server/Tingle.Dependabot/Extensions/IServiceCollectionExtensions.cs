﻿using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Tingle.Dependabot;
using Tingle.Dependabot.Workflow;

namespace Microsoft.Extensions.DependencyInjection;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedLockProvider(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        var configKey = ConfigurationPath.Combine("DistributedLocking", "FilePath");

        var path = configuration.GetValue<string?>(configKey);

        // when the path is null in development, set one
        if (string.IsNullOrWhiteSpace(path) && environment.IsDevelopment())
        {
            path = Path.Combine(environment.ContentRootPath, "locks");
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

        services.AddScoped<ConfigFilesWriter>();

        services.AddSingleton<CertificateManager>();
        services.AddHostedService<CertificateManagerInitializerService>();

        services.AddScoped<UpdateRunner>();
        services.AddSingleton<UpdateScheduler>();

        services.AddHttpClient<AzureDevOpsProvider>();
        services.AddHttpClient<GitHubGraphClient>();
        services.AddScoped<Synchronizer>();

        return services;
    }

    public static IServiceCollection AddInitialSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InitialSetupOptions>(configuration);
        services.ConfigureOptions<InitialSetupConfigureOptions>();

        services.AddHostedService<InitialSetupService>();

        return services;
    }
}
