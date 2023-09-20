using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tingle.Dependabot.Workflow;

internal class Synchronizer
{
    private readonly MainDbContext dbContext;
    private readonly AzureDevOpsProvider adoProvider;
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    private readonly IDeserializer yamlDeserializer;

    public Synchronizer(MainDbContext dbContext, AzureDevOpsProvider adoProvider, IEventPublisher publisher, ILogger<Synchronizer> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.adoProvider = adoProvider ?? throw new ArgumentNullException(nameof(adoProvider));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        yamlDeserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance)
                                                    .IgnoreUnmatchedProperties()
                                                    .Build();
    }

    public async Task SynchronizeAsync(Project project, bool trigger, CancellationToken cancellationToken = default)
    {
        // TODO: skip if the project last synchronization is less than 1 hour ago

        // track the synchronization pairs
        var syncPairs = new List<(SynchronizerConfigurationItem, Repository?)>();

        // get the repositories from Azure
        logger.LogDebug("Listing repositories ...");
        var adoRepos = await adoProvider.GetRepositoriesAsync(project, cancellationToken);
        logger.LogDebug("Found {RepositoriesCount} repositories", adoRepos.Count);
        var adoReposMap = adoRepos.ToDictionary(r => r.Id.ToString(), r => r);

        // synchronize each project
        foreach (var (adoRepositoryId, adoRepo) in adoReposMap)
        {
            // skip disabled or fork repositories
            if (adoRepo.IsDisabled is true || adoRepo.IsFork)
            {
                logger.LogInformation("Skipping sync for {RepositoryName} because it is disabled or is a fork", adoRepo.Name);
                continue;
            }

            // get the repository from the database
            var adoRepositoryName = adoRepo.Name;
            var repository = await (from r in dbContext.Repositories
                                    where r.ProjectId == project.Id
                                    where r.ProviderId == adoRepositoryId
                                    select r).SingleOrDefaultAsync(cancellationToken);

            var item = await adoProvider.GetConfigurationFileAsync(project: project,
                                                                   repositoryIdOrName: adoRepositoryId,
                                                                   cancellationToken: cancellationToken);

            // Track for further synchronization
            var sci = new SynchronizerConfigurationItem(((AzureDevOpsProjectUrl)project.Url!).MakeRepositorySlug(adoRepo.Name), adoRepo, item);
            syncPairs.Add((sci, repository));
        }

        // remove repositories that are no longer tracked (i.e. the repository was removed)
        var providerIdsToKeep = syncPairs.Where(p => p.Item1.HasConfiguration).Select(p => p.Item1.Id).ToList();
        var deleted = await dbContext.Repositories.Where(r => !providerIdsToKeep.Contains(r.ProviderId!)).ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} repositories that are no longer present in the project.", deleted);
        }

        // synchronize each repository
        foreach (var (pi, repository) in syncPairs)
        {
            await SynchronizeAsync(project, repository, pi, trigger, cancellationToken);
        }
    }

    public async Task SynchronizeAsync(Project project, Repository repository, bool trigger, CancellationToken cancellationToken = default)
    {
        // get repository
        var adoRepo = await adoProvider.GetRepositoryAsync(project: project,
                                                           repositoryIdOrName: repository.ProviderId!,
                                                           cancellationToken: cancellationToken);

        // skip disabled or fork repository
        if (adoRepo.IsDisabled is true || adoRepo.IsFork)
        {
            logger.LogInformation("Skipping sync for {RepositoryName} because it is disabled or is a fork", adoRepo.Name);
            return;
        }

        // get the configuration file
        var item = await adoProvider.GetConfigurationFileAsync(project: project,
                                                               repositoryIdOrName: repository.ProviderId!,
                                                               cancellationToken: cancellationToken);

        // perform synchronization
        var sci = new SynchronizerConfigurationItem(((AzureDevOpsProjectUrl)project.Url!).MakeRepositorySlug(adoRepo.Name), adoRepo, item);
        await SynchronizeAsync(project, repository, sci, trigger, cancellationToken);
    }

    public async Task SynchronizeAsync(Project project, string? repositoryProviderId, bool trigger, CancellationToken cancellationToken = default)
    {
        // get repository
        var adoRepo = await adoProvider.GetRepositoryAsync(project: project,
                                                           repositoryIdOrName: repositoryProviderId!,
                                                           cancellationToken: cancellationToken);

        // skip disabled or fork repository
        if (adoRepo.IsDisabled is true || adoRepo.IsFork)
        {
            logger.LogInformation("Skipping sync for {RepositoryName} because it is disabled or is a fork", adoRepo.Name);
            return;
        }

        // get the configuration file
        var item = await adoProvider.GetConfigurationFileAsync(project: project,
                                                               repositoryIdOrName: repositoryProviderId!,
                                                               cancellationToken: cancellationToken);

        var repository = await (from r in dbContext.Repositories
                                where r.ProviderId == repositoryProviderId
                                select r).SingleOrDefaultAsync(cancellationToken);

        // perform synchronization
        var sci = new SynchronizerConfigurationItem(((AzureDevOpsProjectUrl)project.Url!).MakeRepositorySlug(adoRepo.Name), adoRepo, item);
        await SynchronizeAsync(project, repository, sci, trigger, cancellationToken);
    }

    internal async Task SynchronizeAsync(Project project,
                                         Repository? repository,
                                         SynchronizerConfigurationItem providerInfo,
                                         bool trigger,
                                         CancellationToken cancellationToken = default)
    {
        // ensure not null (can be null when deleted and an event is sent)
        if (!providerInfo.HasConfiguration)
        {
            // delete repository
            if (repository is not null)
            {
                logger.LogInformation("Deleting '{RepositorySlug}' as it no longer has a configuration file.", repository.Slug);
                dbContext.Repositories.Remove(repository);
                await dbContext.SaveChangesAsync(cancellationToken);

                // publish RepositoryDeletedEvent event
                var evt = new RepositoryDeletedEvent { ProjectId = project.Id, RepositoryId = repository.Id, };
                await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
            }

            return;
        }

        // check if the file changed (different commit)
        bool commitChanged = true; // assume changes unless otherwise
        var commitId = providerInfo.CommitId;
        if (repository is not null)
        {
            commitChanged = !string.Equals(commitId, repository.LatestCommit);
        }

        // create repository
        RepositoryCreatedEvent? rce = null;
        if (repository is null)
        {
            repository = new Repository
            {
                Id = Guid.NewGuid().ToString("n"),
                Created = DateTimeOffset.UtcNow,
                ProjectId = project.Id,
                ProviderId = providerInfo.Id,
            };
            await dbContext.Repositories.AddAsync(repository, cancellationToken);
            rce = new RepositoryCreatedEvent { ProjectId = project.Id, RepositoryId = repository.Id, };
        }

        // if the name of the repository has changed then we assume the commit changed so that we update stuff
        if (repository.Name != providerInfo.Name) commitChanged = true;

        if (commitChanged)
        {
            logger.LogDebug("Configuration file for '{RepositorySlug}' is new or has been updated.", repository.Slug);

            // set/update existing values
            repository.Updated = DateTimeOffset.UtcNow;
            repository.Name = providerInfo.Name;
            repository.Slug = providerInfo.Slug;
            repository.LatestCommit = commitId;
            repository.ConfigFileContents = providerInfo.Content;

            try
            {
                var configuration = yamlDeserializer.Deserialize<DependabotConfiguration>(repository.ConfigFileContents);
                RecursiveValidator.ValidateObjectRecursive(configuration);

                // set the registries
                repository.Registries = configuration.Registries;

                // set the updates a fresh
                var updates = configuration.Updates!;
                repository.Updates = updates.Select(update => new RepositoryUpdate(update)
                {
                    Files = new List<string>(), // files are populated by an API call from Ruby during job execution

                    LatestJobId = null,
                    LatestJobStatus = null,
                    LatestUpdate = null,
                }).ToList();
            }
            catch (YamlDotNet.Core.YamlException ye)
            {
                logger.LogWarning(ye, "Skipping '{RepositorySlug}'. The YAML file is invalid.", repository.Slug);
                repository.SyncException = ye.Message;
            }
            catch (ValidationException ve)
            {
                logger.LogWarning(ve, "Configuration file for '{RepositorySlug}' is invalid.", repository.Slug);
                repository.SyncException = ve.Message;
            }

            // Update the database
            await dbContext.SaveChangesAsync(cancellationToken);

            // publish RepositoryCreatedEvent or RepositoryUpdatedEvent event
            if (rce is not null)
            {
                await publisher.PublishAsync(rce, cancellationToken: cancellationToken);
            }
            else
            {
                var evt = new RepositoryUpdatedEvent { ProjectId = project.Id, RepositoryId = repository.Id, };
                await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
            }

            if (trigger)
            {
                // trigger update jobs for the whole repository
                var evt = new TriggerUpdateJobsEvent
                {
                    ProjectId = project.Id,
                    RepositoryId = repository.Id,
                    RepositoryUpdateId = null, // run all
                    Trigger = UpdateJobTrigger.Synchronization,
                };
                await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
            }
        }
    }
}
