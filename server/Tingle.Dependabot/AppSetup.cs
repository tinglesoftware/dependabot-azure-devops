using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;

namespace Tingle.Dependabot;

internal static class AppSetup
{
    public static async Task SetupAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider;

        // perform migrations on startup if asked to
        if (app.Configuration.GetValue<bool>("EFCORE_PERFORM_MIGRATIONS"))
        {
            var db = provider.GetRequiredService<MainDbContext>().Database;
            if (db.IsRelational()) // only relational databases
            {
                await db.MigrateAsync(cancellationToken: cancellationToken);
            }
        }

        var options = provider.GetRequiredService<IOptions<WorkflowOptions>>().Value;
        if (options.SynchronizeOnStartup)
        {
            var synchronizer = provider.GetRequiredService<Synchronizer>();
            await synchronizer.SynchronizeAsync(false, cancellationToken); /* database sync should not trigger, just in case it's too many */
        }

        if (options.LoadSchedulesOnStartup)
        {
            var dbContext = provider.GetRequiredService<MainDbContext>();
            var repositories = await dbContext.Repositories.ToListAsync(cancellationToken);
            var scheduler = provider.GetRequiredService<UpdateScheduler>();
            foreach (var repository in repositories)
            {
                await scheduler.CreateOrUpdateAsync(repository, cancellationToken);
            }
        }

        // create or update webhooks/subscriptions if asked to
        if (options.CreateOrUpdateWebhooksOnStartup)
        {
            var adoProvider = provider.GetRequiredService<AzureDevOpsProvider>();
            await adoProvider.CreateOrUpdateSubscriptionsAsync(cancellationToken);
        }
    }
}
