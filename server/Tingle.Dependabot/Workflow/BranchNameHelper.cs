using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Tingle.Dependabot.Workflow;

public static class BranchNameHelper
{
    public static string GetBranchNameForUpdate(string packageEcosystem,
                                                string targetBranchName,
                                                string directory,
                                                string? dependencyGroupName,
                                                List<Models.Azure.PullRequestStoredDependency> dependencies,
                                                string? separator)
    {
        string branchName;

        // updates for groups may result in a long branch name
        if (dependencyGroupName is not null || dependencies.Count > 1)
        {
            // Group/multi dependency update
            // e.g. dependabot/nuget/main/microsoft-3b49c54d9e
            var data = string.Join(",", dependencies.Select(dep => $"{dep.Name}-{dep.Version}"));
            var hash = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(data)))[..10];
            branchName = $"{(string.IsNullOrEmpty(dependencyGroupName) ? "multi" : dependencyGroupName)}-{hash}";
        }
        else
        {
            // Single dependency update
            // e.g. dependabot/nuget/main/Microsoft.Extensions.Logging-1.0.0
            var dependencyNames = string.Join("-and-", dependencies.Select(dep => dep.Name
                .Replace(":", "-")
                .Replace("[", "-")
                .Replace("]", "-")
                .Replace("@", "")));

            var versionSuffix = dependencies[0].Removed is true ? "removed" : dependencies[0].Version;
            branchName = $"{dependencyNames}-{versionSuffix}";
        }

        // TODO: Add config for the branch prefix? Task V1 supported this via DEPENDABOT_BRANCH_NAME_PREFIX
        return SanitizeRef(["dependabot", packageEcosystem, targetBranchName, directory, branchName], separator ?? "/");
    }

    private static string SanitizeRef(string[] refParts, string separator)
    {
        var joined = string.Join(separator, refParts.Where(p => !string.IsNullOrWhiteSpace(p)));

        // Remove forbidden characters
        joined = Regex.Replace(joined, @"[^A-Za-z0-9\/\-_.(){}]", "");
        // Slashes can't be followed by periods
        joined = Regex.Replace(joined, @"\/\.", "/dot-");
        // Squeeze out consecutive periods and slashes
        joined = Regex.Replace(joined, @"\.+", ".");
        joined = Regex.Replace(joined, @"\/+", "/");
        // Remove trailing periods
        joined = Regex.Replace(joined, @"\.$", "");

        return joined;
    }
}