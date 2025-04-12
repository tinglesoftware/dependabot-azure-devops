using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;

namespace Tingle.Dependabot.Controllers;

[ApiController]
[Route("update_jobs")]
[Authorize(AuthConstants.PolicyNameUpdater)]
public class UpdateJobsController(MainDbContext dbContext, IEventPublisher publisher, ILogger<UpdateJobsController> logger) : ControllerBase // TODO: unit and integration test this
{
    // TODO: implement logic for *pull_request endpoints

    [HttpPost("{id}/create_pull_request")]
    public async Task<IActionResult> CreatePullRequestAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotCreatePullRequest> model)
    {
        var (project, repository, job, _) = await GetEntitiesAsync(id);

        logger.LogInformation("Received request to create a pull request from job {JobId} but we did nothing.\r\n{ModelJson}", job.Id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/update_pull_request")]
    public async Task<IActionResult> UpdatePullRequestAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotUpdatePullRequest> model)
    {
        var (project, repository, job, _) = await GetEntitiesAsync(id);

        logger.LogInformation("Received request to update a pull request from job {JobId} but we did nothing.\r\n{ModelJson}", job.Id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/close_pull_request")]
    public async Task<IActionResult> ClosePullRequestAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotClosePullRequest> model)
    {
        var (project, repository, job, _) = await GetEntitiesAsync(id);

        logger.LogInformation("Received request to close a pull request from job {JobId} but we did nothing.\r\n{ModelJson}", job.Id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/record_update_job_error")]
    public async Task<IActionResult> RecordErrorAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotRecordUpdateJobError> model)
    {
        var (_, _, job, _) = await GetEntitiesAsync(id);

        job.Error = new UpdateJobError
        {
            Type = model.Data!.ErrorType,
            Detail = model.Data.ErrorDetails,
        };

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/record_update_job_unknown_error")]
    public async Task<IActionResult> RecordUpdateJobUnknownErrorAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotRecordUpdateJobUnknownError> model)
    {
        var (_, _, job, _) = await GetEntitiesAsync(id);

        job.Error = new UpdateJobError
        {
            Type = model.Data!.ErrorType,
            Detail = model.Data.ErrorDetails,
        };

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPatch("{id}/mark_as_processed")]
    public async Task<IActionResult> MarkAsProcessedAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotMarkAsProcessed> model)
    {
        var (_, _, job, _) = await GetEntitiesAsync(id);

        // the update jobs needs sometime to exit after calling this endpoint, usually up to 1 minute
        // we publish an event in the future that will run update the job and collect logs
        var evt = new UpdateJobCheckStateEvent { JobId = job.Id, };
        var scheduleTime = DateTimeOffset.UtcNow.AddMinutes(1.5f);
        await publisher.PublishAsync(evt, scheduleTime);

        return Ok();
    }

    [HttpPost("{id}/update_dependency_list")]
    public async Task<IActionResult> UpdateDependencyListAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotUpdateDependencyList> model)
    {
        var (_, _, _, update) = await GetEntitiesAsync(id);

        // update the database
        if (update is not null)
        {
            update.Files = model.Data?.DependencyFiles ?? [];
        }
        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/record_ecosystem_versions")]
    public async Task<IActionResult> RecordEcosystemVersionsAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotRecordEcosystemVersions> model)
    {
        var (_, _, job, _) = await GetEntitiesAsync(id);
        logger.LogInformation("Received request to record ecosystem version from job {JobId} but we did nothing.\r\n{ModelJson}", job.Id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/increment_metric")]
    public async Task<IActionResult> IncrementMetricAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotIncrementMetric> model)
    {
        var (_, _, job, _) = await GetEntitiesAsync(id);
        logger.LogInformation("Received metrics from job {JobId} but we did nothing with them.\r\n{ModelJson}", job.Id, JsonSerializer.Serialize(model));
        return Ok();
    }

    private record Entities(Project Project, Repository Repository, UpdateJob Job, RepositoryUpdate? update);
    private async Task<Entities> GetEntitiesAsync(string id, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id, cancellationToken);
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == job.RepositoryId, cancellationToken);
        var project = await dbContext.Projects.SingleAsync(p => p.Id == repository.ProjectId, cancellationToken);
        var update = repository.GetUpdate(job);

        return new Entities(project, repository, job, update);
    }
}
