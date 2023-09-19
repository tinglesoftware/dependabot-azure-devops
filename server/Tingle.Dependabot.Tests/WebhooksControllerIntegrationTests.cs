using AspNetCore.Authentication.Basic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.EventBus;
using Tingle.EventBus.Transports.InMemory;
using Xunit;
using Xunit.Abstractions;

namespace Tingle.Dependabot.Tests;

public class WebhooksControllerIntegrationTests
{
    private readonly ITestOutputHelper outputHelper;

    public WebhooksControllerIntegrationTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Fact]
    public async Task Returns_Unauthorized()
    {
        await TestAsync(async (harness, client) =>
        {
            // without Authorization header
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            Assert.Empty(await harness.PublishedAsync());

            // password does not match what is on record
            request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump5")));
            response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    [Fact]
    public async Task Returns_BadRequest_NoBody()
    {
        await TestAsync(async (harness, client) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"type\":\"https://tools.ietf.org/html/rfc7231#section-6.5.1\"", body);
            Assert.Contains("\"title\":\"One or more validation errors occurred.\"", body);
            Assert.Contains("\"status\":400", body);
            Assert.Contains("\"errors\":{\"\":[\"A non-empty request body is required.\"],\"model\":[\"The model field is required.\"]}", body);
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    [Fact]
    public async Task Returns_BadRequest_MissingValues()
    {
        await TestAsync(async (harness, client) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"type\":\"https://tools.ietf.org/html/rfc7231#section-6.5.1\"", body);
            Assert.Contains("\"title\":\"One or more validation errors occurred.\"", body);
            Assert.Contains("\"status\":400", body);
            Assert.Contains("\"SubscriptionId\":[\"The SubscriptionId field is required.\"]", body);
            Assert.Contains("\"EventType\":[\"The EventType field is required.\"]", body);
            Assert.Contains("\"Resource\":[\"The Resource field is required.\"]", body);
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    [Fact]
    public async Task Returns_UnsupportedMediaType()
    {
        await TestAsync(async (harness, client) =>
        {
            var stream = TestSamples.GetAzureDevOpsPullRequestUpdated1();
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StreamContent(stream);
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"type\":\"https://tools.ietf.org/html/rfc7231#section-6.5.13\"", body);
            Assert.Contains("\"title\":\"Unsupported Media Type\"", body);
            Assert.Contains("\"status\":415", body);
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    [Fact]
    public async Task Returns_OK_CodePush()
    {
        await TestAsync(async (harness, client) =>
        {
            var stream = TestSamples.GetAzureDevOpsGitPush1();
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());

            // Ensure the message was published
            var context = Assert.IsType<EventContext<ProcessSynchronization>>(Assert.Single(await harness.PublishedAsync(TimeSpan.FromSeconds(1f))));
            var inner = context.Event;
            Assert.NotNull(inner);
            Assert.Null(inner.RepositoryId);
            Assert.Equal("278d5cd2-584d-4b63-824a-2ba458937249", inner.RepositoryProviderId);
            Assert.True(inner.Trigger);
        });
    }

    [Fact]
    public async Task Returns_OK_PullRequestUpdated()
    {
        await TestAsync(async (harness, client) =>
        {
            var stream = TestSamples.GetAzureDevOpsPullRequestUpdated1();
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    [Fact]
    public async Task Returns_OK_PullRequestMerged()
    {
        await TestAsync(async (harness, client) =>
        {
            var stream = TestSamples.GetAzureDevOpsPullRequestMerged1();
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    [Fact]
    public async Task Returns_OK_PullRequestCommentEvent()
    {
        await TestAsync(async (harness, client) =>
        {
            var stream = TestSamples.GetAzureDevOpsPullRequestCommentEvent1();
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/azure");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("vsts:burp-bump")));
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    private async Task TestAsync(Func<InMemoryTestHarness, HttpClient, Task> executeAndVerify)
    {
        // Arrange
        var builder = new WebHostBuilder()
            .ConfigureLogging(builder => builder.AddXUnit(outputHelper))
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Schemes:ServiceHooks:Credentials:vsts"] = "burp-bump",
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddControllers()
                        .AddApplicationPart(typeof(MainDbContext).Assembly)
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.AllowTrailingCommas = true;
                            options.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
                        });

                var dbName = Guid.NewGuid().ToString();
                var configuration = context.Configuration;
                services.AddDbContext<MainDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName, o => o.EnableNullChecks());
                    options.EnableDetailedErrors();
                });
                services.AddRouting();

                services.AddAuthentication()
                        .AddBasic<BasicUserValidationService>(AuthConstants.SchemeNameServiceHooks, options => options.Realm = "Dependabot");

                services.AddAuthorization(options =>
                {
                    options.AddPolicy(AuthConstants.PolicyNameServiceHooks, policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthConstants.SchemeNameServiceHooks)
                              .RequireAuthenticatedUser();
                    });
                });

                services.AddEventBus(builder => builder.AddInMemoryTransport().AddInMemoryTestHarness());
            })
            .Configure(app =>
            {
                app.UseRouting();

                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });
        using var server = new TestServer(builder);

        using var scope = server.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<MainDbContext>();
        await context.Database.EnsureCreatedAsync();

        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.StartAsync();

        try
        {
            var client = server.CreateClient();

            await executeAndVerify(harness, client);

            // Ensure there were no publish failures
            Assert.Empty(await harness.FailedAsync());
        }
        finally
        {
            await harness.StopAsync();
        }
    }
}
