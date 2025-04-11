using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Workflow;

internal partial class ConfigFilesWriter(IOptions<WorkflowOptions> optionsAccessor,
                                         IOptions<JsonOptions> jsonOptionsAccessor,
                                         ILogger<ConfigFilesWriter> logger)
{
    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    private readonly JsonOptions jsonOptions = jsonOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(jsonOptionsAccessor));

    public async Task WriteJobAsync(string path,
                                    Project project,
                                    RepositoryUpdate update,
                                    UpdateJob job,
                                    IReadOnlyList<IReadOnlyDictionary<string, string>> credentials,
                                    bool updatingPullRequest,
                                    string? updateDependencyGroupName,
                                    IList<string>? updateDependencyNames,
                                    bool debug,
                                    CancellationToken cancellationToken = default)
    {
        // write the job definition file
        using var stream = File.OpenWrite(path);
        await WriteJobAsync(stream: stream,
                            project: project,
                            update: update,
                            job: job,
                            credentials: credentials,
                            updatingPullRequest: updatingPullRequest,
                            updateDependencyGroupName: updateDependencyGroupName,
                            updateDependencyNames: updateDependencyNames,
                            debug: debug,
                            cancellationToken: cancellationToken);
        logger.WrittenJobDefinitionFile(job.Id, path);
    }

    public async Task WriteJobAsync(Stream stream,
                                    Project project,
                                    RepositoryUpdate update,
                                    UpdateJob job,
                                    IReadOnlyList<IReadOnlyDictionary<string, string>> credentials,
                                    bool updatingPullRequest,
                                    string? updateDependencyGroupName,
                                    IList<string>? updateDependencyNames,
                                    bool debug,
                                    CancellationToken cancellationToken = default)
    {
        // prepare credentials metadata
        var credentialsMetadata = MakeCredentialsMetadata(credentials);

        // prepare the experiments
        var experiments = project.Experiments;
        if (experiments is null || experiments.Count == 0) experiments = new(options.DefaultExperiments);

        // make the definition
        var url = project.Url;
        var definition = new Dictionary<string, object?>
        {
            ["job"] = new Dictionary<string, object?>
            {
                ["package-manager"] = ConvertEcosystemToPackageManager(update.PackageEcosystem!),
                ["updating-a-pull-request"] = updatingPullRequest,
                ["dependency-group-to-refresh"] = updateDependencyGroupName,
                ["dependency-groups"] = (update.Groups ?? []).Select(p => MapDependencyGroup(p.Key, p.Value)),
                ["dependencies"] = updateDependencyNames,
                ["allowed-updates"] = GetAllowDependencies(update.Allow, update.SecurityOnly),
                ["ignore-conditions"] = (update.Ignore ?? []).Select(MapIgnoreDependency),
                ["security-updates-only"] = update.SecurityOnly,
                ["security-advisories"] = Array.Empty<string>(), // TODO: needs mapping similar to the extension
                ["source"] = new Dictionary<string, object?>
                {
                    ["provider"] = "azure",
                    ["api-endpoint"] = new UriBuilder
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Host = url.Hostname,
                        Port = url.Port ?? -1,
                    }.ToString(),
                    ["hostname"] = url.Hostname,
                    ["repo"] = job.RepositorySlug,
                    ["branch"] = update.TargetBranch,
                    ["commit"] = null, // use latest commit of target branch
                    ["directory"] = update.Directory,
                    ["directories"] = update.Directories,
                },
                ["existing-pull-requests"] = Array.Empty<string>(), // TODO: filter out PRs for the given dependency-group-name similar to the extension
                ["existing-group-pull-requests"] = Array.Empty<string>(), // TODO: filter out PRs for the given dependency-group-name similar to the extension
                ["commit-message-options"] = MapCommitMessage(update.CommitMessage),
                ["experiments"] = MapExperiments(experiments),
                ["reject-external-code"] = string.Equals(update.InsecureExternalCodeExecution, "deny"),
                ["repo-private"] = null, // TODO: add config for this?
                ["repo-contents-path"] = null, // TODO: add config for this?
                ["requirements-update-strategy"] = MapRequirementsUpdateStrategy(update.VersioningStrategy),
                ["lockfile-only"] = update.VersioningStrategy == "lockfile-only",
                ["vendor-dependencies"] = update.Vendor,
                ["debug"] = debug,
                ["credentials-metadata"] = credentialsMetadata,
                ["update-subdependencies"] = false,

                // TODO: Investigate if these options are needed or are now obsolete.
                //       These options don't appear to be used by dependabot-core yet/anymore,
                //       but do appear in GitHub Dependabot job logs seen in the wild.
                // ["max-updater-run-time"] = 2700,
                // ["proxy-log-response-body-on-auth-failure"] = true,
            },
            // credentials do not go to the updater, just the metadata
        };

        // serialize the job definition
        await JsonSerializer.SerializeAsync(stream, definition, jsonOptions.SerializerOptions, cancellationToken);
    }

    public async Task WriteProxyAsync(string path,
                                      UpdateJob job,
                                      IReadOnlyList<IReadOnlyDictionary<string, string>> credentials,
                                      CertificateAuthority ca,
                                      CancellationToken cancellationToken)
    {
        // write the proxy config file
        using var stream = File.OpenWrite(path);
        await WriteProxyAsync(stream, credentials, ca, cancellationToken);
        logger.WrittenProxyConfigFile(job.Id, path);
    }

    public async Task WriteProxyAsync(Stream stream,
                                      IReadOnlyList<IReadOnlyDictionary<string, string>> credentials,
                                      CertificateAuthority ca,
                                      CancellationToken cancellationToken)
    {
        var config = new Dictionary<string, object>
        {
            ["all_credentials"] = credentials,
            // CertificateAuthority includes the MITM CA certificate and private key
            ["ca"] = new Dictionary<string, string>
            {
                ["cert"] = ca.Cert,
                ["key"] = ca.Key,
            },
        };

        // serialize the job definition
        await JsonSerializer.SerializeAsync(stream, config, jsonOptions.SerializerOptions, cancellationToken);
    }

    public IReadOnlyList<IReadOnlyDictionary<string, string>> MakeCredentials(Project project, Repository repository, RepositoryUpdate update)
    {
        // prepare credentials with replaced secrets
        var secrets = new Dictionary<string, string>(project.Secrets) { ["DEFAULT_TOKEN"] = project.Token!, };
        var registries = update.Registries?.Select(r => repository.Registries[r]).ToList() ?? [];
        return MakeCredentials(registries, secrets, project, options.GithubToken);
    }

    internal static IReadOnlyList<IReadOnlyDictionary<string, string>> MakeCredentialsMetadata(IReadOnlyList<IReadOnlyDictionary<string, string>> credentials)
    {
        var metadata = new List<IReadOnlyDictionary<string, string>>();

        // remove the sensitive values
        string[] sensitive = ["username", "token", "password", "key", "auth-key"];
        foreach (var cred in credentials)
        {
            metadata.Add(
                new Dictionary<string, string>(
                    cred.Where(k => !sensitive.Contains(k.Key, StringComparer.OrdinalIgnoreCase))));
        }

        return metadata;
    }
    internal static IReadOnlyList<IReadOnlyDictionary<string, string>> MakeCredentials(IReadOnlyCollection<DependabotRegistry> registries, IReadOnlyDictionary<string, string> secrets, Project project, string? githubToken)
    {
        var credentials = new List<IReadOnlyDictionary<string, string>>()
        {
            new Dictionary< string, string>
            {
                ["type"] = "git_source",
                ["host"] = project.Url.Hostname,
                ["username"] = "x-access-token",
                ["password"] = project.Token!,
            },
        };

        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            credentials.Add(new Dictionary<string, string>
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

            var values = new Dictionary<string, string> { ["type"] = type, };

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
    internal static string? ConvertEcosystemToPackageManager(string ecosystem)
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
    internal static IReadOnlyDictionary<string, object?> MapDependencyGroup(string name, DependabotGroupDependency group)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = name,
            ["applies-to"] = group.AppliesTo,
            ["rules"] = new Dictionary<string, object?>
            {
                ["patterns"] = group.Patterns,
                ["exclude-patterns"] = group.ExcludePatterns,
                ["dependency-type"] = group.DependencyType,
                ["update-types"] = group.UpdateTypes,
            },
        };
    }
    internal static IReadOnlyDictionary<string, object?>? MapCommitMessage(DependabotCommitMessage? message)
    {
        if (message is null) return null;
        return new Dictionary<string, object?>
        {
            ["prefix"] = message.Prefix,
            ["prefix-development"] = message.PrefixDevelopment,
            ["include-scope"] = message.Include == "scope" ? true : null,
        };
    }
    internal static IReadOnlyDictionary<string, object> MapExperiments(IReadOnlyDictionary<string, string> experiments)
    {
        // Experiment values are known to be either 'true', 'false', or a string value.
        // If the value is 'true' or 'false', convert it to a boolean type so that dependabot-core handles it correctly.

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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
    internal static IReadOnlyList<IReadOnlyDictionary<string, string?>> GetAllowDependencies(List<DependabotAllowDependency>? allow, bool securityOnly)
    {
        // If no allow conditions are specified, update direct dependencies by default; This is what GitHub does.
        // NOTE: 'update-type' appears to be a deprecated config, but still appears in the dependabot-core model and GitHub Dependabot job logs.
        //       See: https://github.com/dependabot/dependabot-core/blob/b3a0c1f86c20729494097ebc695067099f5b4ada/updater/lib/dependabot/job.rb#L253C1-L257C78

        if (allow is null)
        {
            return [new Dictionary<string, string?>
            {
                ["dependency-type"] = "direct",
                ["update-type"] = securityOnly ? "security" : "all",
            }];
        }

        return allow.Select(c => new Dictionary<string, string?>
        {
            ["dependency-name"] = c.DependencyName,
            ["dependency-type"] = c.DependencyType,
            ["update-type"] = c.UpdateType,
        }).ToList();
    }
    internal static IReadOnlyDictionary<string, object?> MapIgnoreDependency(DependabotIgnoreDependency ignore)
    {
        return new Dictionary<string, object?>
        {
            // https://github.com/dependabot/cli/blob/main/internal/infra/job.go
            ["source"] = null,
            ["updated-at"] = null,
            ["dependency-name"] = ignore.DependencyName,
            ["update-types"] = ignore.UpdateTypes,

            // The dependabot.yml config docs are not very clear about acceptable values; after scanning dependabot-core and dependabot-cli,
            // this could either be a single version string (e.g. '>1.0.0'), or multiple version strings separated by commas (e.g. '>1.0.0, <2.0.0')
            ["version-requirement"] = ignore.Versions switch
            {
                System.Text.Json.Nodes.JsonArray arr => string.Join(",", arr.GetValues<string>()),
                System.Text.Json.Nodes.JsonValue jv => jv.GetValue<string>(),
                _ => null,
            },
        };
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
