using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;

namespace Tingle.Dependabot.Controllers;

[ApiController]
[Route("/update_jobs")]
[Authorize(AuthConstants.PolicyNameUpdater)]
public class UpdateJobsController : ControllerBase // TODO: unit and integration test this
{
    private readonly MainDbContext dbContext;
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    public UpdateJobsController(MainDbContext dbContext, IEventPublisher publisher, ILogger<UpdateJobsController> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // TODO: implement logic for *pull_request endpoints

    [HttpPost("{id}/create_pull_request")]
    public async Task<IActionResult> CreatePullRequestAsync([FromRoute, Required] string id, [FromBody] PayloadWithData<DependabotCreatePullRequestModel> model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == job.RepositoryId);
        var project = await dbContext.Projects.SingleAsync(p => p.Id == job.ProjectId);

        logger.LogInformation("Received request to create a pull request from job {JobId} but we did nothing.\r\n{ModelJson}", id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/update_pull_request")]
    public async Task<IActionResult> UpdatePullRequestAsync([FromRoute, Required] string id, [FromBody] PayloadWithData<DependabotUpdatePullRequestModel> model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == job.RepositoryId);
        var project = await dbContext.Projects.SingleAsync(p => p.Id == job.ProjectId);

        logger.LogInformation("Received request to update a pull request from job {JobId} but we did nothing.\r\n{ModelJson}", id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/close_pull_request")]
    public async Task<IActionResult> ClosePullRequestAsync([FromRoute, Required] string id, [FromBody] PayloadWithData<DependabotClosePullRequestModel> model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == job.RepositoryId);
        var project = await dbContext.Projects.SingleAsync(p => p.Id == job.ProjectId);

        logger.LogInformation("Received request to close a pull request from job {JobId} but we did nothing.\r\n{ModelJson}", id, JsonSerializer.Serialize(model));
        return Ok();
    }

    [HttpPost("{id}/record_update_job_error")]
    public async Task<IActionResult> RecordErrorAsync([FromRoute, Required] string id, [FromBody] PayloadWithData<DependabotRecordUpdateJobErrorModel> model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);

        job.Error = new UpdateJobError
        {
            Type = model.Data!.ErrorType,
            Detail = model.Data.ErrorDetail,
        };

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPatch("{id}/mark_as_processed")]
    public async Task<IActionResult> MarkAsProcessedAsync([FromRoute, Required] string id, [FromBody] PayloadWithData<DependabotMarkAsProcessedModel> model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);

        // publish event that will run update the job and collect logs
        var evt = new UpdateJobCheckStateEvent { JobId = id, };
        await publisher.PublishAsync(evt);

        return Ok();
    }

    [HttpPost("{id}/update_dependency_list")]
    public async Task<IActionResult> UpdateDependencyListAsync([FromRoute, Required] string id, [FromBody] PayloadWithData<DependabotUpdateDependencyListModel> model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == job.RepositoryId);

        // update the database
        var update = repository.Updates.SingleOrDefault(u => u.PackageEcosystem == job.PackageEcosystem && u.Directory == job.Directory);
        if (update is not null)
        {
            update.Files = model.Data?.DependencyFiles ?? new();
        }
        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/record_ecosystem_versions")]
    public async Task<IActionResult> RecordEcosystemVersionsAsync([FromRoute, Required] string id, [FromBody] JsonNode model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);
        logger.LogInformation("Received request to record ecosystem version from job {JobId} but we did nothing.\r\n{ModelJson}", id, model.ToJsonString());
        return Ok();
    }

    [HttpPost("{id}/increment_metric")]
    public async Task<IActionResult> IncrementMetricAsync([FromRoute, Required] string id, [FromBody] JsonNode model)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id);
        logger.LogInformation("Received metrics from job {JobId} but we did nothing with them.\r\n{ModelJson}", id, model.ToJsonString());
        return Ok();
    }
}
