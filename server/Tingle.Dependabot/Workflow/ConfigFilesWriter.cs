using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using SC = Tingle.Dependabot.DependabotSerializerContext;

namespace Tingle.Dependabot.Workflow;

internal partial class ConfigFilesWriter(CertificateManager certificateManager,
                                         IOptions<WorkflowOptions> optionsAccessor,
                                         ILogger<ConfigFilesWriter> logger)
{
    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    public async Task WriteJobAsync(string path, WriteJobParams @params, CancellationToken cancellationToken = default)
    {
        // write the job definition file
        using var stream = File.OpenWrite(path);
        await WriteJobAsync(stream, @params, cancellationToken);
        logger.WrittenJobDefinitionFile(@params.Job.Id, path);
    }
    public async Task WriteJobAsync(Stream stream, WriteJobParams @params, CancellationToken cancellationToken = default)
    {
        // prepare credentials metadata
        var credentialsMetadata = MakeCredentialsMetadata(@params.Credentials);

        // prepare the experiments
        var project = @params.Project;
        var experiments = project.Experiments;
        if (experiments is null || experiments.Count == 0) experiments = new(options.DefaultExperiments);

        // make the definition
        var url = project.Url;
        var update = @params.Update;
        var job = @params.Job;
        var definition = new DependabotJobConfig(
            PackageManager: ConvertEcosystemToPackageManager(update.PackageEcosystem!),
            AllowedUpdates: GetAllowDependencies(update.Allow, update.SecurityOnly),
            Debug: @params.Debug,
            DependencyGroups: [.. (update.Groups ?? []).Select(p => MapDependencyGroup(p.Key, p.Value))],
            Dependencies: @params.UpdateDependencyNames,
            DependencyGroupToRefresh: @params.UpdateDependencyGroupName,
            ExistingPullRequests: [], // TODO: filter out PRs for the given dependency-group-name similar to the extension
            ExistingGroupPullRequests: [], // TODO: filter out PRs for the given dependency-group-name similar to the extension
            Experiments: MapExperiments(experiments),
            IgnoreConditions: [.. (update.Ignore ?? []).Select(MapIgnoreDependency)],
            LockfileOnly: update.VersioningStrategy == "lockfile-only",
            RequirementsUpdateStrategy: MapRequirementsUpdateStrategy(update.VersioningStrategy),
            SecurityAdvisories: [], // TODO: needs mapping similar to the extension
            SecurityUpdatesOnly: update.SecurityOnly,
            Source: new DependabotSource(
                Provider: "azure",
                Repo: job.RepositorySlug,
                Directory: update.Directory,
                Directories: update.Directories,
                Branch: update.TargetBranch,
                Commit: null, // use latest commit of target branch
                Hostname: url.Hostname,
                APIEndpoint: new UriBuilder
                {
                    Scheme = Uri.UriSchemeHttps,
                    Host = url.Hostname,
                    Port = url.Port ?? -1,
                }.ToString()
            ),
            UpdateSubdependencies: false,
            UpdatingAPullRequest: @params.UpdatingPullRequest,
            VendorDependencies: update.Vendor,
            RejectExternalCode: string.Equals(update.InsecureExternalCodeExecution, "deny"),
            RepoPrivate: null, // TODO: add config for this?
            CommitMessageOptions: MapCommitMessage(update.CommitMessage),
            // credentials do not go to the updater, just the metadata
            CredentialsMetadata: credentialsMetadata
        // MaxUpdaterRunTime: 2700
        );

        // serialize the job config
        var config = new DependabotJobFile(definition);
        await JsonSerializer.SerializeAsync(stream, config, SC.Default.DependabotJobFile, cancellationToken);
    }

    public async Task WriteProxyAsync(string path, WriteConfigParams @params, CancellationToken cancellationToken = default)
    {
        // write the proxy config file
        using var stream = File.OpenWrite(path);
        await WriteProxyAsync(stream, @params, cancellationToken);
        logger.WrittenProxyConfigFile(@params.Job.Id, path);
    }
    public async Task WriteProxyAsync(Stream stream, WriteConfigParams @params, CancellationToken cancellationToken = default)
    {
        var ca = certificateManager.Get();
        var config = new DependabotProxyConfig(@params.Credentials, ca);

        // serialize the proxy config
        await JsonSerializer.SerializeAsync(stream, config, SC.Default.DependabotProxyConfig, cancellationToken);
    }

    public IReadOnlyList<DependabotCredential> MakeCredentials(Project project, Repository repository, RepositoryUpdate update)
    {
        // prepare credentials with replaced secrets
        var secrets = new Dictionary<string, string>(project.Secrets) { ["DEFAULT_TOKEN"] = project.Token!, };
        var registries = update.Registries?.Select(r => repository.Registries[r]).ToList() ?? [];
        return MakeCredentials(registries, secrets, project, options.GithubToken);
    }

    internal static IReadOnlyList<DependabotCredential> MakeCredentialsMetadata(IReadOnlyList<DependabotCredential> credentials)
    {
        var metadata = new List<DependabotCredential>();

        // remove the sensitive values
        string[] sensitive = ["username", "token", "password", "key", "auth-key"];
        foreach (var cred in credentials)
        {
            metadata.Add(
                new DependabotCredential(
                    cred.Where(k => !sensitive.Contains(k.Key, StringComparer.OrdinalIgnoreCase))));
        }

        return metadata;
    }
    internal static IReadOnlyList<DependabotCredential> MakeCredentials(IReadOnlyCollection<DependabotRegistry> registries, IReadOnlyDictionary<string, string> secrets, Project project, string? githubToken)
    {
        var credentials = new List<DependabotCredential>()
        {
            new DependabotCredential
            {
                ["type"] = "git_source",
                ["host"] = project.Url.Hostname,
                ["username"] = "x-access-token",
                ["password"] = project.Token!,
            },
        };

        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            credentials.Add(new DependabotCredential
            {
                ["type"] = "git_source",
                ["host"] = "github.com",
                ["username"] = "x-access-token",
                ["password"] = githubToken,
            });
        }

        foreach (var v in registries)
        {
            var type = v.Type?.Replace("-", "_") ?? throw new InvalidOperationException("Type should not be null");

            var values = new DependabotCredential { ["type"] = type, };

            // values for hex-organization
            values.AddIfNotDefault("organization", v.Organization);

            // values for hex-repository
            values.AddIfNotDefault("repo", v.Repo);
            values.AddIfNotDefault("auth-key", v.AuthKey);
            values.AddIfNotDefault("public-key-fingerprint", v.PublicKeyFingerprint);

            values.AddIfNotDefault("username", v.Username);
            values.AddIfNotDefault("password", ConvertPlaceholder(v.Password, secrets));
            values.AddIfNotDefault("key", ConvertPlaceholder(v.Key, secrets));
            values.AddIfNotDefault("token", ConvertPlaceholder(v.Token, secrets));
            values.AddIfNotDefault("replaces-base", v.ReplacesBase is true ? "true" : null);

            /*
             * Some credentials do not use the 'url' property in the Ruby updater.
             * The 'host' and 'registry' properties are derived from the given URL.
             * The 'registry' property is derived from the 'url' by stripping off the scheme.
             * The 'host' property is derived from the hostname of the 'url'.
             *
             * 'npm_registry' and 'docker_registry' use 'registry' only.
             * 'terraform_registry' uses 'host' only.
             * 'composer_repository' uses both 'url' and 'host'.
             * 'python_index' uses 'index-url' instead of 'url'.
            */

            if (Uri.TryCreate(v.Url, UriKind.Absolute, out var url))
            {
                var addRegistry = type is "docker_registry" or "npm_registry";
                if (addRegistry) values.Add("registry", $"{url.Host}{url.PathAndQuery}".TrimEnd('/'));

                var addHost = type is "terraform_registry" or "composer_repository";
                if (addHost) values.Add("host", url.Host);
            }

            if (type is "python_index") values.AddIfNotDefault("index-url", v.Url);

            var skipUrl = type is "docker_registry" or "npm_registry" or "terraform_registry" or "python_index";
            if (!skipUrl) values.AddIfNotDefault("url", v.Url);

            credentials.Add(values);
        }

        return credentials;
    }
    internal static string? ConvertPlaceholder(string? input, IReadOnlyDictionary<string, string> secrets)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var result = input;
        var matches = PlaceholderPattern().Matches(input);
        foreach (var m in matches)
        {
            if (m is not Match match || !match.Success) continue;

            var placeholder = match.Value;
            var name = match.Groups[1].Value;
            if (secrets.TryGetValue(name, out var replacement))
            {
                result = result.Replace(placeholder, replacement);
            }
        }

        return result;
    }
    internal static string ConvertEcosystemToPackageManager(string ecosystem)
    {
        ArgumentException.ThrowIfNullOrEmpty(ecosystem);

        return ecosystem switch
        {
            "dotnet-sdk" => "dotnet_sdk",
            "github-actions" => "github_actions",
            "gitsubmodule" => "submodules",
            "gomod" => "go_modules",
            "mix" => "hex",
            "npm" => "npm_and_yarn",
            // Additional ones
            "yarn" => "npm_and_yarn",
            "pnpm" => "npm_and_yarn",
            "pipenv" => "pip",
            "pip-compile" => "pip",
            "poetry" => "pip",
            _ => ecosystem,
        };
    }
    internal static DependabotGroup MapDependencyGroup(string name, DependabotGroupDependency group)
    {
        return new DependabotGroup(
            GroupName: name,
            AppliesTo: group.AppliesTo,
            Rules: new System.Text.Json.Nodes.JsonObject
            {
                ["patterns"] = JsonSerializer.Serialize(group.Patterns, SC.Default.ListString),
                ["exclude-patterns"] = JsonSerializer.Serialize(group.ExcludePatterns, SC.Default.ListString),
                ["dependency-type"] = group.DependencyType,
                ["update-types"] = JsonSerializer.Serialize(group.UpdateTypes, SC.Default.ListString),
            }
        );
    }
    internal static DependabotCommitOptions? MapCommitMessage(DependabotCommitMessage? message)
    {
        if (message is null) return null;
        return new DependabotCommitOptions(
            Prefix: message.Prefix,
            PrefixDevelopment: message.PrefixDevelopment,
            IncludeScope: message.Include == "scope" ? true : null
        );
    }
    internal static DependabotExperiment MapExperiments(IReadOnlyDictionary<string, string> experiments)
    {
        // Experiment values are known to be either 'true', 'false', or a string value.
        // If the value is 'true' or 'false', convert it to a boolean type so that dependabot-core handles it correctly.

        var result = new DependabotExperiment(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in experiments)
        {
            if (value is string str)
            {
                if (string.Equals(str, "true", StringComparison.OrdinalIgnoreCase)) result[key] = true;
                else if (string.Equals(str, "false", StringComparison.OrdinalIgnoreCase)) result[key] = false;
                else result[key] = str;
            }
            else result[key] = value;
        }

        return result;
    }
    internal static IReadOnlyList<DependabotAllowed> GetAllowDependencies(List<DependabotAllowDependency>? allow, bool securityOnly)
    {
        // If no allow conditions are specified, update direct dependencies by default; This is what GitHub does.
        // NOTE: 'update-type' appears to be a deprecated config, but still appears in the dependabot-core model and GitHub Dependabot job logs.
        //       See: https://github.com/dependabot/dependabot-core/blob/b3a0c1f86c20729494097ebc695067099f5b4ada/updater/lib/dependabot/job.rb#L253C1-L257C78

        if (allow is null)
        {
            return [new DependabotAllowed(
                DependencyType: "direct",
                UpdateType: securityOnly ? "security" : "all"
            )];
        }

        return [.. allow.Select(c => new DependabotAllowed(
            DependencyName: c.DependencyName,
            DependencyType: c.DependencyType,
            UpdateType: c.UpdateType
        ))];
    }
    internal static DependabotCondition MapIgnoreDependency(DependabotIgnoreDependency ignore)
    {
        return new DependabotCondition(
            DependencyName: ignore.DependencyName!,
            Source: null,
            UpdateTypes: ignore.UpdateTypes,
            UpdatedAt: null,

            // The dependabot.yml config docs are not very clear about acceptable values; after scanning dependabot-core and dependabot-cli,
            // this could either be a single version string (e.g. '>1.0.0'), or multiple version strings separated by commas (e.g. '>1.0.0, <2.0.0')
            VersionRequirement: ignore.Versions switch
            {
                System.Text.Json.Nodes.JsonArray arr => string.Join(",", arr.GetValues<string>()),
                System.Text.Json.Nodes.JsonValue jv => jv.GetValue<string>(),
                _ => null,
            }
        );
    }
    internal static string? MapRequirementsUpdateStrategy(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value switch
        {
            "auto" => null,
            "increase" => "bump_versions",
            "increase-if-necessary" => "bump_versions_if_necessary",
            "lockfile-only" => "lockfile_only",
            "widen" => "widen_ranges",
            _ => throw new InvalidOperationException($"Versioning strategy: '{value}' is not supported"),
        };
    }

    [GeneratedRegex("\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9_-]*)\\s*}}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderPattern();
}

internal class WriteJobParams
{
    public required Project Project { get; init; }
    public required RepositoryUpdate Update { get; init; }
    public required UpdateJob Job { get; init; }
    public required IReadOnlyList<DependabotCredential> Credentials { get; init; }
    public bool UpdatingPullRequest { get; init; }
    public string? UpdateDependencyGroupName { get; init; }
    public required List<string> UpdateDependencyNames { get; init; }
    public bool Debug { get; init; }
}

internal class WriteConfigParams
{
    public required UpdateJob Job { get; init; }
    public required IReadOnlyList<DependabotCredential> Credentials { get; init; }
}
