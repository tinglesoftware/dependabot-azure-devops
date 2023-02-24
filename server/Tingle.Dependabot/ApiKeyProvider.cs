using AspNetCore.Authentication.ApiKey;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tingle.Dependabot.Models;

namespace Tingle.Dependabot;

internal class ApiKeyProvider : IApiKeyProvider
{
    private readonly MainDbContext dbContext;

    public ApiKeyProvider(MainDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IApiKey?> ProvideAsync(string key)
    {
        var job = await dbContext.UpdateJobs.SingleOrDefaultAsync(j => j.AuthKey == key);
        if (job is not null)
        {
            return new ApiKey(key, job.RepositoryId!);
        }

        return null;
    }

    class ApiKey : IApiKey
    {
        public ApiKey(string key, string owner, IReadOnlyCollection<Claim>? claims = null)
        {
            Key = key;
            OwnerName = owner;
            Claims = claims ?? new List<Claim>();
        }

        public string Key { get; }
        public string OwnerName { get; }
        public IReadOnlyCollection<Claim> Claims { get; }
    }
}
