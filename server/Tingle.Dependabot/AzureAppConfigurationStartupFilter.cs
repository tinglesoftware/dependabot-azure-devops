namespace Tingle.Dependabot;

internal class AzureAppConfigurationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            var configuration = builder is WebApplication webApp
                              ? webApp.Configuration
                              : builder.ApplicationServices.GetRequiredService<IConfiguration>();

            /*
             * If the endpoint is not null, then AppConfiguration services were added.
             * This means we can add the middleware to the pipeline.
             * Otherwise it would throw an exception.
             */
            var endpoint = configuration.GetValue<Uri?>("AzureAppConfig:Endpoint");
            if (endpoint is not null)
            {
                builder.UseAzureAppConfiguration();
            }

            next(builder);
        };
    }
}
