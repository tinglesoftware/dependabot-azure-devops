using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Controllers;

[ApiController]
[Route("update_jobs")]
[Authorize(AuthConstants.PolicyNameUpdater)]
public class UpdateJobsController(MainDbContext dbContext, ILogger<UpdateJobsController> logger) : ControllerBase // TODO: unit and integration test this
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
    public IActionResult MarkAsProcessedAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotMarkAsProcessed> model) => Ok();

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
    public IActionResult RecordEcosystemVersionsAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotRecordEcosystemVersions> model) => Ok();

    [HttpPost("{id}/increment_metric")]
    public IActionResult IncrementMetricAsync([FromRoute] string id, [FromBody] DependabotRequest<DependabotIncrementMetric> model) => Ok();

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
