namespace Microsoft.AspNetCore.Http;

internal static class HttpContextExtensions
{
    public const string XProjectId = "X-Project-Id";

    public static string? GetProjectId(this HttpContext httpContext)
        => httpContext.Request.Headers.TryGetValue(XProjectId, out var values) ? values.Single() : null;
}
