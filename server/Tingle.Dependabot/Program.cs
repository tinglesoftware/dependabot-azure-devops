using AspNetCore.Authentication.ApiKey;
using AspNetCore.Authentication.Basic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniValidation;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tingle.Dependabot;
using Tingle.Dependabot.Consumers;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);

// Add DbContext
builder.Services.AddDbContext<MainDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sql"), options => options.EnableRetryOnFailure());
    options.EnableDetailedErrors();
});
// restore this once the we no longer pull schedules from DB on startup
//builder.Services.AddDatabaseMigrator<MainDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add data protection
builder.Services.AddDataProtection().PersistKeysToDbContext<MainDbContext>();

// Add controllers
builder.Services.AddControllers()
                .AddControllersAsServices()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.AllowTrailingCommas = true;
                    options.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
                });

// Configure any generated URL to be in lower case
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddAuthentication()
                .AddJwtBearer(AuthConstants.SchemeNameManagement)
                .AddApiKeyInAuthorizationHeader<ApiKeyProvider>(AuthConstants.SchemeNameUpdater, options => options.Realm = "Dependabot")
                .AddBasic<BasicUserValidationService>(AuthConstants.SchemeNameServiceHooks, options => options.Realm = "Dependabot");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.PolicyNameManagement, policy =>
    {
        policy.AddAuthenticationSchemes(AuthConstants.SchemeNameManagement)
              .RequireAuthenticatedUser();
    });

    options.AddPolicy(AuthConstants.PolicyNameServiceHooks, policy =>
    {
        policy.AddAuthenticationSchemes(AuthConstants.SchemeNameServiceHooks)
              .RequireAuthenticatedUser();
    });

    options.AddPolicy(AuthConstants.PolicyNameUpdater, policy =>
    {
        policy.AddAuthenticationSchemes(AuthConstants.SchemeNameUpdater)
              .RequireAuthenticatedUser();
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// Configure other services
builder.Services.AddWorkflowServices(builder.Configuration.GetSection("Workflow"));

// Add event bus
var selectedTransport = builder.Configuration.GetValue<EventBusTransportKind?>("EventBus:SelectedTransport");
builder.Services.AddEventBus(builder =>
{
    // Setup consumers
    builder.AddConsumer<ProcessSynchronizationConsumer>();
    builder.AddConsumer<RepositoryEventsConsumer>();
    builder.AddConsumer<TriggerUpdateJobsEventConsumer>();
    builder.AddConsumer<UpdateJobEventsConsumer>();

    // Setup transports
    var credential = new Azure.Identity.DefaultAzureCredential();
    if (selectedTransport is EventBusTransportKind.ServiceBus)
    {
        builder.AddAzureServiceBusTransport(
            options => ((AzureServiceBusTransportCredentials)options.Credentials).TokenCredential = credential);
    }
    else if (selectedTransport is EventBusTransportKind.InMemory)
    {
        builder.AddInMemoryTransport();
    }
});

// Add health checks
builder.Services.AddHealthChecks()
                .AddDbContextCheck<MainDbContext>();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/liveness", new HealthCheckOptions { Predicate = _ => false, });
app.MapControllers();
app.MapManagementApi();

// setup the application environment
await AppSetup.SetupAsync(app);

await app.RunAsync();

internal enum EventBusTransportKind { InMemory, ServiceBus, }

internal static class ApplicationExtensions
{
    public static IServiceCollection AddWorkflowServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkflowOptions>(configuration);
        services.ConfigureOptions<WorkflowConfigureOptions>();

        services.AddSingleton<UpdateRunner>();
        services.AddSingleton<UpdateScheduler>();

        services.AddScoped<AzureDevOpsProvider>();
        services.AddScoped<Synchronizer>();
        services.AddHostedService<WorkflowBackgroundService>();

        return services;
    }

    public static IEndpointRouteBuilder MapManagementApi(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("");
        group.RequireAuthorization(AuthConstants.PolicyNameManagement);

        group.MapPost("/sync", async (IEventPublisher publisher, [FromBody] SynchronizationRequest model) =>
        {
            // request synchronization of the project
            var evt = new ProcessSynchronization(model.Trigger);
            await publisher.PublishAsync(evt);

            return Results.Ok();
        });

        group.MapPost("/webhooks/register/azure", async (AzureDevOpsProvider adoProvider) =>
        {
            await adoProvider.CreateOrUpdateSubscriptionsAsync();
            return Results.Ok();
        });

        group.MapGet("repos", async (MainDbContext dbContext) => Results.Ok(await dbContext.Repositories.ToListAsync()));
        group.MapGet("repos/{id}", async (MainDbContext dbContext, [FromRoute, Required] string id) => Results.Ok(await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id)));
        group.MapPost("repos/{id}/sync", async (IEventPublisher publisher, MainDbContext dbContext, [FromRoute, Required] string id, [FromBody] SynchronizationRequest model) =>
        {
            if (!MiniValidator.TryValidate(model, out var errors)) return Results.ValidationProblem(errors);

            // ensure repository exists
            var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
            if (repository is null)
            {
                return Results.Problem(title: "repository_not_found", statusCode: 400);
            }

            // request synchronization of the repository
            var evt = new ProcessSynchronization(model.Trigger, repositoryId: repository.Id, null);
            await publisher.PublishAsync(evt);

            return Results.Ok(repository);
        });
        group.MapGet("repos/{id}/jobs/{jobId}", async (MainDbContext dbContext, [FromRoute, Required] string id, [FromRoute, Required] string jobId) =>
        {
            // ensure repository exists
            var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
            if (repository is null)
            {
                return Results.Problem(title: "repository_not_found", statusCode: 400);
            }

            // find the job
            var job = dbContext.UpdateJobs.Where(j => j.RepositoryId == repository.Id && j.Id == jobId).SingleOrDefaultAsync();
            return Results.Ok(job);
        });
        group.MapPost("repos/{id}/trigger", async (IEventPublisher publisher, MainDbContext dbContext, [FromRoute, Required] string id, [FromBody] TriggerUpdateRequest model) =>
        {
            if (!MiniValidator.TryValidate(model, out var errors)) return Results.ValidationProblem(errors);

            // ensure repository exists
            var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == id);
            if (repository is null)
            {
                return Results.Problem(title: "repository_not_found", statusCode: 400);
            }

            // ensure the repository update exists
            var update = repository.Updates.ElementAtOrDefault(model.Id!.Value);
            if (update is null)
            {
                return Results.Problem(title: "repository_update_not_found", statusCode: 400);
            }

            // trigger update for specific update
            var evt = new TriggerUpdateJobsEvent
            {
                RepositoryId = repository.Id,
                RepositoryUpdateId = model.Id.Value,
                Trigger = UpdateJobTrigger.Manual,
            };
            await publisher.PublishAsync(evt);

            return Results.Ok(repository);
        });

        return builder;
    }
}
