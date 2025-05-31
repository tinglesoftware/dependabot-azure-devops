using AspNetCore.Authentication.ApiKey;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tingle.Dependabot.Models;

namespace Tingle.Dependabot;

internal class ApiKeyProvider(IHttpContextAccessor httpContextAccessor, MainDbContext dbContext) : IApiKeyProvider
{
    public async Task<IApiKey?> ProvideAsync(string key)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var job = await dbContext.UpdateJobs.SingleOrDefaultAsync(j => j.AuthKey == key);
        if (job is not null && httpContext is not null && httpContext.Request.Path.ToString().Contains($"/{job.Id}/"))
        {
            return new ApiKey(key, job.RepositoryId!);
        }

        return null;
    }

    class ApiKey(string key, string owner, IReadOnlyCollection<Claim>? claims = null) : IApiKey
    {
        public string Key { get; } = key;
        public string OwnerName { get; } = owner;
        public IReadOnlyCollection<Claim> Claims { get; } = claims ?? new List<Claim>();
    }
}
