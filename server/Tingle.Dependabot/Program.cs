using AspNetCore.Authentication.ApiKey;
using AspNetCore.Authentication.Basic;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tingle.Dependabot;
using Tingle.Dependabot.Consumers;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.PeriodicTasks;
using Tingle.Dependabot.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30)); /* default is 5 seconds */

// Add OpenTelemetry
builder.AddOpenTelemetry();
builder.Services.AddHttpContextAccessor(); // needed by custom enrichers

// Add Azure AppConfiguration
builder.Configuration.AddStandardAzureAppConfiguration(builder.Environment);
builder.Services.AddAzureAppConfiguration();
builder.Services.AddSingleton<IStartupFilter, AzureAppConfigurationStartupFilter>(); // Use IStartupFilter to setup AppConfiguration middleware correctly

// Add DbContext
builder.Services.AddDbContext<MainDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sql"), options => options.EnableRetryOnFailure());
    options.EnableDetailedErrors();
});
builder.Services.AddDatabaseSetup<MainDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add controllers
builder.Services.AddControllers()
                .AddControllersAsServices()
                .AddJsonOptions(options => options.JsonSerializerOptions.UseStandard());

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UseStandard());

// Configure any generated URL to be in lower case
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddAuthentication()
                .AddApiKeyInAuthorizationHeader<ApiKeyProvider>(AuthConstants.SchemeNameUpdater, options =>
                {
                    options.KeyName = "Bearer"; // "Authorization: Bearer <token>" or "Authorization: Updater <token>" will work
                    options.Realm = "Dependabot";
                })
                .AddBasic<BasicUserValidationService>(AuthConstants.SchemeNameServiceHooks, options => options.Realm = "Dependabot");

builder.Services.AddAuthorizationBuilder()
                .AddPolicy(AuthConstants.PolicyNameServiceHooks, policy =>
                {
                    policy.AddAuthenticationSchemes(AuthConstants.SchemeNameServiceHooks)
                          .RequireAuthenticatedUser();
                })
                .AddPolicy(AuthConstants.PolicyNameUpdater, policy =>
                {
                    policy.AddAuthenticationSchemes(AuthConstants.SchemeNameUpdater)
                          .RequireAuthenticatedUser();
                });

// Configure other services
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddStandardFeatureManagement();
builder.Services.AddDistributedLockProvider(builder.Environment, builder.Configuration);
builder.Services.AddWorkflowServices(builder.Configuration.GetSection("Workflow"));

// Add event bus
var selectedTransport = builder.Configuration.GetValue<EventBusTransportKind?>("EventBus:SelectedTransport");
builder.Services.AddEventBus(builder =>
{
    // Setup consumers
    builder.AddConsumer<ProcessSynchronizationConsumer>();
    builder.AddConsumer<RepositoryEventsConsumer>();
    builder.AddConsumer<RunUpdateJobEventConsumer>();

    // Setup transports
    var credential = new Azure.Identity.DefaultAzureCredential();
    if (selectedTransport is EventBusTransportKind.ServiceBus)
    {
        builder.AddAzureServiceBusTransport(options =>
        {
            ((AzureServiceBusTransportCredentials)options.Credentials).TokenCredential = credential;

            options.SetupQueueOptions = (reg, opt) =>
            {
                if (reg.EventType == typeof(Tingle.Dependabot.Events.RunUpdateJobEvent))
                {
                    // an update job can run for up to 2700 seconds (add 2 minutes afterwards)
                    opt.LockDuration = TimeSpan.FromSeconds(2700) + TimeSpan.FromMinutes(2);
                }
            };
            options.SetupProcessorOptions = (reg, _, opt) =>
            {
                if (reg.EventType == typeof(Tingle.Dependabot.Events.RunUpdateJobEvent))
                {
                    opt.MaxAutoLockRenewalDuration = Timeout.InfiniteTimeSpan; // needs to be longer than LockDuration
                    opt.MaxConcurrentCalls = 20; // run up to 20 update jobs concurrently (this should be configurable)
                }
            };
        });
    }
    else if (selectedTransport is EventBusTransportKind.InMemory)
    {
        builder.AddInMemoryTransport();
    }
});

builder.Services.AddPeriodicTasks(builder =>
{
    builder.AddTask<MissedTriggerCheckerTask>(schedule: "8 * * * *"); // every hour at minute 8
    builder.AddTask<UpdateJobsCleanerTask>(schedule: "*/15 * * * *"); // every 15 minutes
    builder.AddTask<SynchronizationTask>(schedule: "23 */6 * * *"); // every 6 hours at minute 23
});

// Add health checks
builder.Services.AddHealthChecks()
                .AddDbContextCheck<MainDbContext>();

// Add service to do initial sync (must be after the IHostedService for migrations)
builder.Services.AddInitialSetup(builder.Configuration.GetSection("InitialSetup"));

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/liveness", new HealthCheckOptions { Predicate = _ => false, });
app.MapControllers();

await app.RunAsync();

internal enum EventBusTransportKind { InMemory, ServiceBus, }
