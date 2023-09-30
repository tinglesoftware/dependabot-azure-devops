using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.FeatureManagement.Mvc;

namespace Tingle.Dependabot;

internal class CustomDisabledFeaturesHandler : IDisabledFeaturesHandler
{
    /// <inheritdoc/>
    public Task HandleDisabledFeatures(IEnumerable<string> features, ActionExecutingContext context)
    {
        var title = ErrorCodes.FeaturesDisabled;
        var detail = $"The required features '{string.Join(", ", features)}' are disabled";

        IActionResult? result = null;
        if (context.Controller is ControllerBase cb && cb.ProblemDetailsFactory != null)
        {
            result = cb.Problem(title: title, detail: detail, statusCode: 400);
        }

        result ??= new BadRequestObjectResult(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = 400,
        });

        context.Result = result;
        return Task.CompletedTask;
    }
}
