using Azure.Identity;

namespace Microsoft.Extensions.Configuration;

/// <summary>Extensions for <see cref="ConfigurationManager"/>.</summary>
public static class ConfigurationManagerExtensions
{
    /// <summary>
    /// Adds standard Azure AppConfiguration provider and services.
    /// </summary>
    /// <param name="manager">The <see cref="ConfigurationManager"/> to be configured.</param>
    /// <param name="environment">The <see cref="IHostEnvironment"/> to use.</param>
    /// <returns></returns>
    public static ConfigurationManager AddStandardAzureAppConfiguration(this ConfigurationManager manager, IHostEnvironment environment)
    {
        var section = manager.GetSection("AzureAppConfig");
        var endpoint = section.GetValue<Uri?>("Endpoint");
        var label = section.GetValue<string?>("Label") ?? environment.EnvironmentName;
        if (endpoint is not null)
        {
            var cacheExpiration = section.GetValue<TimeSpan?>("CacheExpiration") ?? TimeSpan.FromHours(1);
            var cacheExpirationInterval = section.GetValue<TimeSpan?>("CacheExpirationInterval") ?? TimeSpan.FromMinutes(30);
            manager.AddAzureAppConfiguration(options =>
            {
                options.Connect(endpoint: endpoint, credential: new DefaultAzureCredential())
                       .Select("*", label)
                       .ConfigureRefresh(o =>
                       {
                           o.Register("Refresh", label, refreshAll: true) // key to use to trigger refresh
                            .SetCacheExpiration(cacheExpiration);
                       })
                       .UseFeatureFlags(o =>
                       {
                           o.Label = label;
                           o.CacheExpirationInterval = cacheExpirationInterval;
                       });
            });
        }

        return manager;
    }
}
