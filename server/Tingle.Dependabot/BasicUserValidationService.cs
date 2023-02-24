using AspNetCore.Authentication.Basic;

namespace Tingle.Dependabot;

internal class BasicUserValidationService : IBasicUserValidationService
{
    private readonly IConfiguration configuration;

    public BasicUserValidationService(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task<bool> IsValidAsync(string username, string password)
    {
        var expected = configuration.GetValue<string?>($"Authentication:Schemes:ServiceHooks:Credentials:{username}");
        return Task.FromResult(string.Equals(expected, password, StringComparison.Ordinal));
    }
}
