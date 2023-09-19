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
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection("Workflow"));
builder.Services.ConfigureOptions<WorkflowConfigureOptions>();
builder.Services.AddSingleton<UpdateRunner>();
builder.Services.AddSingleton<UpdateScheduler>();
builder.Services.AddScoped<AzureDevOpsProvider>();
builder.Services.AddScoped<Synchronizer>();
builder.Services.AddHostedService<WorkflowBackgroundService>();

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

// setup the application environment
await AppSetup.SetupAsync(app);

await app.RunAsync();

internal enum EventBusTransportKind { InMemory, ServiceBus, }
