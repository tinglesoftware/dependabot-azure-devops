using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Controllers;

[ApiController]
[Route("/mgnt")]
[Authorize(AuthConstants.PolicyNameManagement)]
public class ManagementController : ControllerBase // TODO: unit test this
{
    private readonly MainDbContext dbContext;
    private readonly IEventPublisher publisher;
    private readonly AzureDevOpsProvider adoProvider;

    public ManagementController(MainDbContext dbContext, IEventPublisher publisher, AzureDevOpsProvider adoProvider)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.adoProvider = adoProvider ?? throw new ArgumentNullException(nameof(adoProvider));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncAsync([FromBody] SynchronizationRequest model)
    {
        // request synchronization of the project
        var evt = new ProcessSynchronization(model.Trigger);
        await publisher.PublishAsync(evt);

        return Ok();
    }

    [HttpPost("/webhooks/register")]
    public async Task<IActionResult> WebhooksRegisterAsync()
    {
        await adoProvider.CreateOrUpdateSubscriptionsAsync();
        return Ok();
    }

    [HttpGet("repos")]
    public async Task<IActionResult> GetReposAsync()
    {
        var repos = await dbContext.Repositories.ToListAsync();
        return Ok(repos);
    }

    [HttpGet("repos/{id}")]
    public async Task<IActionResult> GetRepoAsync([FromRoute, Required] string id)
    {
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
        return Ok(repository);
    }

    [HttpGet("repos/{id}/jobs/{jobId}")]
    public async Task<IActionResult> GetJobAsync([FromRoute, Required] string id, [FromRoute, Required] string jobId)
    {
        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
        if (repository is null)
        {
            return Problem(title: "repository_not_found", statusCode: 400);
        }

        // find the job
        var job = dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
        return Ok(job);
    }

    [HttpPost("repos/{id}/sync")]
    public async Task<IActionResult> SyncRepoAsync([FromRoute, Required] string id, [FromBody] SynchronizationRequest model)
    {
        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
        if (repository is null)
        {
            return Problem(title: "repository_not_found", statusCode: 400);
        }

        // request synchronization of the repository
        var evt = new ProcessSynchronization(model.Trigger, repositoryId: repository.Id, null);
        await publisher.PublishAsync(evt);

        return Ok(repository);
    }

    [HttpPost("repos/{id}/trigger")]
    public async Task<IActionResult> TriggerAsync([FromRoute, Required] string id, [FromBody] TriggerUpdateRequest model)
    {
        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
        if (repository is null)
        {
            return Problem(title: "repository_not_found", statusCode: 400);
        }

        // ensure the repository update exists
        var update = repository.Updates.ElementAtOrDefault(model.Id!.Value);
        if (update is null)
        {
            return Problem(title: "repository_update_not_found", statusCode: 400);
        }

        // trigger update for specific update
        var evt = new TriggerUpdateJobsEvent
        {
            RepositoryId = repository.Id,
            RepositoryUpdateId = model.Id.Value,
            Trigger = UpdateJobTrigger.Manual,
        };
        await publisher.PublishAsync(evt);

        return Ok(repository);
    }
}
