using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTelemetry.Resources;

namespace Tingle.Dependabot.OpenTelemetry;

internal class GitResourceDetector : IResourceDetector
{
    public Resource Detect()
    {
        var attributeList = new List<KeyValuePair<string, object>>();

        try
        {
            var sha = GetValueFromEnvs(["GITHUB_SHA", "VERCEL_GIT_COMMIT_SHA", "CF_PAGES_COMMIT_SHA"])
                   ?? ExecuteGitCommand("git rev-parse HEAD");
            if (sha is not null) attributeList.Add(new KeyValuePair<string, object>("git.sha", sha));

            var branch = GetValueFromEnvs(["GITHUB_REF_NAME", "VERCEL_GIT_COMMIT_REF", "CF_PAGES_BRANCH"])
                      ?? ExecuteGitCommand("git rev-parse --abbrev-ref HEAD");
            if (branch is not null) attributeList.Add(new KeyValuePair<string, object>("git.branch", branch));
        }
        catch
        {
            return Resource.Empty;
        }

        return new Resource(attributeList);
    }

    private static string? GetValueFromEnvs(string[] names)
        => names.Select(Environment.GetEnvironmentVariable).FirstOrDefault(value => !string.IsNullOrEmpty(value));

    private static string? ExecuteGitCommand(string command)
    {
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = windows ? "cmd.exe" : "/bin/sh",  // cmd for Windows, /bin/sh for Linux/macOS
                Arguments = windows ? $"/c {command}" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return result;
        }
        catch
        {
            return null; // In case of error, return null
        }
    }
}
