using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniValidation;
using Tingle.Dependabot;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Microsoft.AspNetCore.Builder;

public static class ManagementEndpoints
{
    public static IEndpointConventionBuilder MapManagement(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/mgnt").WithGroupName("management");

        group.MapPost("sync", PerformSyncAsync);
        group.MapPost("webhooks/register", WebhooksRegisterAsync);
        group.MapGet("projects", GetProjectsAsync);
        group.MapGet("repos", GetReposAsync);
        group.MapGet("repos/{id}", GetRepoAsync);
        group.MapGet("repos/{id}/jobs", GetJobsAsync);
        group.MapGet("repos/{id}/jobs/{jobId}", GetJobAsync);
        group.MapDelete("repos/{id}/jobs/{jobId}", DeleteJobAsync);
        group.MapGet("repos/{id}/jobs/{jobId}/logs", GetJobLogsAsync);
        group.MapGet("repos/{id}/jobs/{jobId}/flamegraph", GetJobFlameGraphAsync);
        group.MapPost("repos/{id}/sync", SyncRepoAsync);
        group.MapPost("repos/{id}/trigger", TriggerAsync);

        return group;
    }

    private static async Task<IResult> PerformSyncAsync([FromBody] SynchronizationRequest model,
                                                        [FromServices] MainDbContext dbContext,
                                                        [FromServices] IEventPublisher publisher,
                                                        HttpContext context)
    {
        if (!MiniValidator.TryValidate(model, out var errors)) return Results.ValidationProblem(errors);

        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // request synchronization of the project
        var evt = new ProcessSynchronization(projectId, model.Trigger);
        await publisher.PublishAsync(evt);

        return Results.Ok();
    }

    private static async Task<IResult> WebhooksRegisterAsync([FromServices] MainDbContext dbContext,
                                                             [FromServices] IAzureDevOpsProvider adoProvider,
                                                             HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        await adoProvider.CreateOrUpdateSubscriptionsAsync(project);
        return Results.Ok();
    }

    private static async Task<IResult> GetProjectsAsync([FromServices] MainDbContext dbContext, HttpContext context)
    {
        var projects = await dbContext.Projects.ToListAsync();
        projects.ForEach(p => p.Protect());
        return Results.Ok(projects);
    }

    private static async Task<IResult> GetReposAsync([FromServices] MainDbContext dbContext, HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        var repos = await dbContext.Repositories.Where(r => r.ProjectId == project.Id).ToListAsync();
        return Results.Ok(repos);
    }

    private static async Task<IResult> GetRepoAsync([FromRoute] string id,
                                                    [FromServices] MainDbContext dbContext,
                                                    HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        return Results.Ok(repository);
    }

    public static async Task<IResult> GetJobsAsync([FromRoute] string id, [FromServices] MainDbContext dbContext, HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // find the jobs
        var jobs = await dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id).ToListAsync();
        return Results.Ok(jobs);
    }

    private static async Task<IResult> GetJobAsync([FromRoute] string id,
                                                   [FromRoute] string jobId,
                                                   [FromServices] MainDbContext dbContext,
                                                   HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // find the job
        var job = await dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
        return Results.Ok(job);
    }

    private static async Task<IResult> DeleteJobAsync([FromRoute] string id,
                                                      [FromRoute] string jobId,
                                                      [FromServices] MainDbContext dbContext,
                                                      HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // ensure job exists
        var job = await dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
        if (job is null) return Results.Problem(title: ErrorCodes.UpdateJobNotFound, statusCode: 400);

        // delete associated files
        job.DeleteFiles();

        // delete the job
        dbContext.UpdateJobs.Remove(job);
        await dbContext.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> GetJobLogsAsync([FromRoute] string id,
                                                       [FromRoute] string jobId,
                                                       [FromServices] MainDbContext dbContext,
                                                       HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // ensure job exists
        var job = await dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
        if (job is null) return Results.Problem(title: ErrorCodes.UpdateJobNotFound, statusCode: 400);

        // read the file and respond with it
        if (job.LogsPath is null) return Results.NoContent();
        var contents = await File.ReadAllTextAsync(job.LogsPath);
        return Results.Content(contents, System.Net.Mime.MediaTypeNames.Text.Plain);
    }

    private static async Task<IResult> GetJobFlameGraphAsync([FromRoute] string id,
                                                             [FromRoute] string jobId,
                                                             [FromServices] MainDbContext dbContext,
                                                             HttpContext context)
    {
        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // ensure job exists
        var job = await dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
        if (job is null) return Results.Problem(title: ErrorCodes.UpdateJobNotFound, statusCode: 400);

        // read the file and respond with it
        if (job.FlameGraphPath is null) return Results.NoContent();
        var contents = await File.ReadAllTextAsync(job.FlameGraphPath);
        return Results.Content(contents, System.Net.Mime.MediaTypeNames.Text.Html);
    }

    private static async Task<IResult> SyncRepoAsync([FromRoute] string id,
                                                     [FromBody] SynchronizationRequest model,
                                                     [FromServices] MainDbContext dbContext,
                                                     [FromServices] IEventPublisher publisher,
                                                     HttpContext context)
    {
        if (!MiniValidator.TryValidate(model, out var errors)) return Results.ValidationProblem(errors);

        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // request synchronization of the repository
        var evt = new ProcessSynchronization(projectId, model.Trigger, repositoryId: repository.Id, null);
        await publisher.PublishAsync(evt);

        return Results.Accepted();
    }

    private static async Task<IResult> TriggerAsync([FromRoute] string id,
                                                    [FromBody] TriggerUpdateRequest model,
                                                    [FromServices] MainDbContext dbContext,
                                                    [FromServices] IEventPublisher publisher,
                                                    HttpContext context)
    {
        if (!MiniValidator.TryValidate(model, out var errors)) return Results.ValidationProblem(errors);

        // ensure project exists
        var projectId = context.GetProjectId() ?? throw new InvalidOperationException("Project identifier must be provided");
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

        // ensure repository exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == id);
        if (repository is null) return Results.Problem(title: ErrorCodes.RepositoryNotFound, statusCode: 400);

        // ensure the repository update exists
        var update = repository.Updates.ElementAtOrDefault(model.Id);
        if (update is null) return Results.Problem(title: ErrorCodes.RepositoryUpdateNotFound, statusCode: 400);

        // publish event to run the job
        var evt = new RunUpdateJobEvent
        {
            ProjectId = project.Id,
            RepositoryId = repository.Id,
            RepositoryUpdateId = model.Id,
            Trigger = UpdateJobTrigger.Manual,
        };
        await publisher.PublishAsync(evt);

        return Results.Accepted();
    }
}
