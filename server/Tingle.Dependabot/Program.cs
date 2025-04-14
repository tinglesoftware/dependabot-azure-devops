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

var development = builder.Environment.IsDevelopment();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["EFCORE_PERFORM_MIGRATIONS"] = development ? "true" : null,

    ["Logging:LogLevel:Default"] = development ? "Information" : "Debug",
    ["Logging:LogLevel:Microsoft"] = development ? "Warning" : "Information",
    ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
    ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Information",
    ["Logging:LogLevel:System"] = development ? "Information" : null,
    ["Logging:LogLevel:System.Net.Http.HttpClient"] = development ? "Warning" : "Information",
    ["Logging:LogLevel:AspNetCore.Authentication"] = development ? "Warning" : null,
    ["Logging:LogLevel:Tingle.AspNetCore"] = development ? "Warning" : "Information",
    ["Logging:LogLevel:Tingle.EventBus"] = development ? "Warning" : "Information",
    ["Logging:Debug:LogLevel:Default"] = "None",

    ["AllowedHosts"] = "*",

    // ["ConnectionStrings:Sql"] = "Server=(localdb)\\mssqllocaldb;Database=dependabot;Trusted_Connection=True;MultipleActiveResultSets=true",
    ["ConnectionStrings:Sql"] = "Server=localhost,1433;Database=dependabot;User Id=sa;Password=My!P#ssw0rd1;TrustServerCertificate=True;MultipleActiveResultSets=False;",

    ["EventBus:SelectedTransport"] = "InMemory", // InMemory|ServiceBus
    ["EventBus:DefaultTransportWaitStarted"] = "false", // defaults to true which causes startup tasks to hang
    ["EventBus:Naming:UseFullTypeNames"] = "false",
    ["EventBus:Transports:azure-service-bus:FullyQualifiedNamespace"] = "{your_namespace}.servicebus.windows.net",
    ["EventBus:Transports:azure-service-bus:DefaultEntityKind"] = "Queue",

    // if you are debugging, uncomment and update the value
    // "InitialSetup:Projects": "[{\"url\":\"https://dev.azure.com/tingle/dependabot\",\"token\":\"dummy\",\"AutoComplete\":true}]",
    ["InitialSetup:SkipLoadSchedules:"] = development ? "false" : "true",
});

builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30)); /* default is 5 seconds */

// Add OpenTelemetry
builder.AddOpenTelemetry();
builder.Services.AddHttpContextAccessor(); // needed by custom enrichers

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
