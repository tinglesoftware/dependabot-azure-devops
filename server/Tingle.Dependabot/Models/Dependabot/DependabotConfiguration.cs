using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotConfiguration : IValidatableObject
{
    [Required, AllowedValues(2)]
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [Required, MinLength(1)]
    [JsonPropertyName("updates")]
    public List<DependabotUpdate>? Updates { get; set; }

    [JsonPropertyName("registries")]
    public Dictionary<string, DependabotRegistry> Registries { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var updates = Updates ?? [];
        var configured = Registries.Keys;
        var referenced = updates.SelectMany(r => r.Registries ?? []).ToList();

        // ensure there are no configured registries that have not been referenced
        var missingConfiguration = referenced.Except(configured).ToList();
        if (missingConfiguration.Count > 0)
        {
            yield return new ValidationResult($"Referenced registries: '{string.Join(",", missingConfiguration)}' have not been configured in the root of dependabot.yml"); ;
        }

        // ensure there are no registries referenced but not configured
        var missingReferences = configured.Except(referenced).ToList();
        if (missingReferences.Count > 0)
        {
            yield return new ValidationResult($"Registries: '{string.Join(",", missingReferences)}' have not been referenced by any update");
        }
    }
}

public record DependabotUpdate : IValidatableObject
{
    /// <summary>Ecosystem for the update.</summary>
    [Required]
    [JsonPropertyName("package-ecosystem")]
    public string? PackageEcosystem { get; set; }

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("directories")]
    public List<string>? Directories { get; set; }

    [Required]
    [JsonPropertyName("schedule")]
    public DependabotUpdateSchedule? Schedule { get; set; }

    [JsonPropertyName("open-pull-requests-limit")]
    public int OpenPullRequestsLimit { get; set; } = 5;

    [JsonPropertyName("registries")]
    public List<string>? Registries { get; set; }

    [JsonPropertyName("allow")]
    public List<DependabotAllowDependency>? Allow { get; set; }

    [JsonPropertyName("groups")]
    public List<DependabotGroupDependency>? Groups { get; set; }

    [JsonPropertyName("ignore")]
    public List<DependabotIgnoreDependency>? Ignore { get; set; }
    [JsonPropertyName("commit-message")]
    public DependabotCommitMessage? CommitMessage { get; set; }
    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }
    [JsonPropertyName("milestone")]
    public int? Milestone { get; set; } = null;
    [JsonPropertyName("pull-request-branch-name")]
    public DependabotPullRequestBranchName? PullRequestBranchName { get; set; }
    [JsonPropertyName("rebase-strategy")]
    public string RebaseStrategy { get; set; } = "auto";
    [JsonPropertyName("insecure-external-code-execution")]
    public string? InsecureExternalCodeExecution { get; set; }
    [JsonPropertyName("target-branch")]
    public string? TargetBranch { get; set; }
    [JsonPropertyName("vendor")]
    public bool Vendor { get; set; } = false;
    [JsonPropertyName("versioning-strategy")]
    public string VersioningStrategy { get; set; } = "auto";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Directory) && (Directories is null || Directories.Count == 0))
        {
            yield return new ValidationResult(
                errorMessage: "Either 'directory' or 'directories' must be provided",
                memberNames: [nameof(Directory), nameof(Directories)]);
        }
    }
}

public class DependabotUpdateSchedule : IValidatableObject
{
    [Required]
    [JsonPropertyName("interval")]
    public DependabotScheduleInterval? Interval { get; set; }

    [Required]
    [JsonPropertyName("time")]
    public TimeOnly? Time { get; set; } = new TimeOnly(2, 0);

    [Required]
    [JsonPropertyName("day")]
    public DependabotScheduleDay? Day { get; set; } = DependabotScheduleDay.Monday;

    [Required, TimeZone]
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "Etc/UTC";

    // [CronExpression]
    [JsonPropertyName("cronjob")]
    public string? Cronjob { get; set; }

    /// <summary>Generate the appropriate CRON schedule.</summary>
    public CronSchedule GenerateCron()
    {
        if (Interval == DependabotScheduleInterval.Cron)
            return Cronjob ?? throw new InvalidOperationException($"'{nameof(Cronjob)}' cannot be null at this point");

        // format to use:
        // minute, hour, day of month, month, day of week

        var time = Time ?? throw new InvalidOperationException($"'{nameof(Time)}' cannot be null at this point");
        var day = Day ?? throw new InvalidOperationException($"'{nameof(Day)}' cannot be null at this point");
        var cron = $"{time:mm} {time:HH} " + Interval switch
        {
            DependabotScheduleInterval.Daily => "* * 1-5",          // any day of the month, any month, but on weekdays
            DependabotScheduleInterval.Weekly => $"* * {(int)day}", // any day of the month, any month, but on a given day
            DependabotScheduleInterval.Monthly => "1 * *",          // first day of the month, any month, any day of the week
            DependabotScheduleInterval.Quarterly => "1 1,4,7,10 *", // first day of each quarter (January, April, July, and October)
            DependabotScheduleInterval.Semiannually => "1 1,7 *",  // every six months, on the first day of January and July
            DependabotScheduleInterval.Yearly => "1 1 *",           // first day of January
            _ => throw new NotImplementedException(),
        };
        return new CronSchedule(cron);
    }

    // TODO: remove this once CronSchedule supports TryParse or the library has CronExpressionAttribute
    static bool TryParse(string? value, [NotNullWhen(true)] out CronSchedule? expression)
    {
        expression = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var spaces = value.Count(c => c == ' ');
        if (Cronos.CronExpression.TryParse(value, spaces == 5 ? Cronos.CronFormat.IncludeSeconds : Cronos.CronFormat.Standard, out var result))
        {
            expression = result;
            return true;
        }
        return false;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Interval is DependabotScheduleInterval.Cron && !TryParse(Cronjob, out _))
        {
            yield return new ValidationResult(
                errorMessage: "'cronjob' must be a valid CRON expression when 'interval' is set to 'cron'",
                memberNames: [nameof(Interval), nameof(Cronjob)]);
        }
    }
}

public class DependabotGroupDependency
{
    [JsonPropertyName("applies-to")]
    public string? AppliesTo { get; set; }
    [JsonPropertyName("dependency-type")]
    public string? DependencyType { get; set; }
    [JsonPropertyName("patterns")]
    public List<string>? Patterns { get; set; }
    [JsonPropertyName("exclude-patterns")]
    public List<string>? ExcludePatterns { get; set; }
    [JsonPropertyName("update-types")]
    public List<string>? UpdateTypes { get; set; }
}

public class DependabotAllowDependency : IValidatableObject
{
    [JsonPropertyName("dependency-name")]
    public string? DependencyName { get; set; }
    [JsonPropertyName("dependency-type")]
    public string? DependencyType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DependencyName is null && DependencyType is null)
        {
            yield return new ValidationResult("Each entry under 'allow' must have 'dependency-name', 'dependency-type' or both set");
        }
    }
}

public class DependabotIgnoreDependency : IValidatableObject
{
    [JsonPropertyName("dependency-name")]
    public string? DependencyName { get; set; }

    [JsonPropertyName("versions")]
    public IList<string>? Versions { get; set; }

    [JsonPropertyName("update-types")]
    public IList<string>? UpdateTypes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DependencyName is null && Versions is null && UpdateTypes is null)
        {
            yield return new ValidationResult("Each entry under 'ignore' must have one of 'dependency-name', 'versions', or 'update-types' set");
        }
    }
}

public class DependabotCommitMessage
{
    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("prefix-development")]
    public string? PrefixDevelopment { get; set; }

    [JsonPropertyName("include")]
    public string? Include { get; set; }
}

public class DependabotPullRequestBranchName
{
    [Required]
    [AllowedValues("-", "_", "/")]
    [JsonPropertyName("separator")]
    public string? Separator { get; set; }
}

public class DependabotRegistry
{
    [Required]
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [Required, Url]
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [DataType(DataType.Password)]
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("key")]
    [DataType(DataType.Password)]
    public string? Key { get; set; }

    [JsonPropertyName("token")]
    [DataType(DataType.Password)]
    public string? Token { get; set; }

    [JsonPropertyName("replaces-base")]
    public bool? ReplacesBase { get; set; } // keep nullable to prevent issues with database context

    [JsonPropertyName("organization")]
    public string? Organization { get; set; }
    [JsonPropertyName("repo")]
    public string? Repo { get; set; }
    [JsonPropertyName("auth-key")]
    public string? AuthKey { get; set; }
    [JsonPropertyName("public-key-fingerprint")]
    public string? PublicKeyFingerprint { get; set; }
}

public enum DependabotScheduleInterval { Daily, Weekly, Monthly, Quarterly, Semiannually, Yearly, Cron }
public enum DependabotScheduleDay { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, }
