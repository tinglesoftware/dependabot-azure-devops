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
public class ManagementController(MainDbContext dbContext, IEventPublisher publisher, AzureDevOpsProvider adoProvider) : ControllerBase // TODO: unit test this
{
    [HttpPost("sync")]
    public async Task<IActionResult> SyncAsync([FromBody] SynchronizationRequest model)
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // request synchronization of the project
        var evt = new ProcessSynchronization(projectId, model.Trigger);
        await publisher.PublishAsync(evt);

        return Ok();
    }

    [HttpPost("/webhooks/register")]
    public async Task<IActionResult> WebhooksRegisterAsync()
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        await adoProvider.CreateOrUpdateSubscriptionsAsync(project);
        return Ok();
    }

    [HttpGet("repos")]
    public async Task<IActionResult> GetReposAsync()
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        var repos = await dbContext.Repositories.Where(r => r.ProjectId == project.Id).ToListAsync();
        return Ok(repos);
    }

    [HttpGet("repos/{id}")]
    public async Task<IActionResult> GetRepoAsync([FromRoute, Required] string id)
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        return Ok(repository);
    }

    [HttpGet("repos/{id}/jobs/{jobId}")]
    public async Task<IActionResult> GetJobAsync([FromRoute, Required] string id, [FromRoute, Required] string jobId)
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // find the job
        var job = dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
        return Ok(job);
    }

    [HttpPost("repos/{id}/sync")]
    public async Task<IActionResult> SyncRepoAsync([FromRoute, Required] string id, [FromBody] SynchronizationRequest model)
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // request synchronization of the repository
        var evt = new ProcessSynchronization(projectId, model.Trigger, repositoryId: repository.Id, null);
        await publisher.PublishAsync(evt);

        return Ok(repository);
    }

    [HttpPost("repos/{id}/trigger")]
    public async Task<IActionResult> TriggerAsync([FromRoute, Required] string id, [FromBody] TriggerUpdateRequest model)
    {
        // ensure project exists
        var projectId = HttpContext.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // ensure the repository update exists
        var update = repository.Updates.ElementAtOrDefault(model.Id!.Value);
        if (update is null) return Problem(title: ErrorCodes.RepositoryUpdateNotFound, statusCode: 400);

        // trigger update for specific update
        var evt = new TriggerUpdateJobsEvent
        {
            ProjectId = project.Id,
            RepositoryId = repository.Id,
            RepositoryUpdateId = model.Id.Value,
            Trigger = UpdateJobTrigger.Manual,
        };
        await publisher.PublishAsync(evt);

        return Ok(repository);
    }
}
