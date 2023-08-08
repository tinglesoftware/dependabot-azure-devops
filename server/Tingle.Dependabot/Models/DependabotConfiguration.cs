using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Tingle.Dependabot.Models;

public class DependabotConfiguration
{
    [Required, AllowedValues(2)]
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [Required, MinLength(1)]
    [JsonPropertyName("updates")]
    public List<DependabotUpdate>? Updates { get; set; }

    [JsonPropertyName("registries")]
    public Dictionary<string, DependabotRegistry>? Registries { get; set; }
}

public record DependabotUpdate
{
    /// <summary>Ecosystem for the update.</summary>
    [Required]
    [JsonPropertyName("package-ecosystem")]
    public DependabotPackageEcosystem? PackageEcosystem { get; set; }

    [Required]
    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [Required]
    [JsonPropertyName("schedule")]
    public DependabotUpdateSchedule? Schedule { get; set; }

    [Required]
    [JsonPropertyName("open-pull-requests-limit")]
    public int? OpenPullRequestsLimit { get; set; } = 5;

    [JsonPropertyName("allow")]
    public List<DependabotAllowDependency>? Allow { get; set; }
    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }
    [JsonPropertyName("milestone")]
    public int? Milestone { get; set; } = null;
    [JsonPropertyName("pull-request-branch-name")]
    public DependabotPullRequestBranchName? PullRequestBranchName { get; set; }
    [JsonPropertyName("rebase-strategy")]
    public DependabotRebaseStrategy RebaseStrategy { get; set; } = DependabotRebaseStrategy.Auto;
    [JsonPropertyName("insecure-external-code-execution")]
    public DependabotInsecureExternalCodeExecution? InsecureExternalCodeExecution { get; set; }
    [JsonPropertyName("target-branch")]
    public string? TargetBranch { get; set; }
    [JsonPropertyName("vendor")]
    public bool Vendor { get; set; } = false;
    [JsonPropertyName("versioning-strategy")]
    public DependabotVersioningStrategy VersioningStrategy { get; set; } = DependabotVersioningStrategy.Auto;
}

public class DependabotUpdateSchedule
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

    /// <summary>Generate the appropriate CRON schedule.</summary>
    public string GenerateCron()
    {
        // format to use:
        // minute, hour, day of month, month, day of week

        var time = Time ?? throw new InvalidOperationException($"'{nameof(Time)}' cannot be null at this point");
        var day = Day ?? throw new InvalidOperationException($"'{nameof(Day)}' cannot be null at this point");
        return $"{time:mm} {time:HH} " + Interval switch
        {
            DependabotScheduleInterval.Daily => "* * 1-5",          // any day of the month, any month, but on weekdays
            DependabotScheduleInterval.Weekly => $"* * {(int)day}", // any day of the month, any month, but on a given day
            DependabotScheduleInterval.Monthly => "1 * *",          // first day of the month, any month, any day of the week
            _ => throw new NotImplementedException(),
        };
    }
}

public class DependabotAllowDependency
{
    [JsonPropertyName("dependency-name")]
    public string? DependencyName { get; set; }
    [JsonPropertyName("dependency-type")]
    public DependabotDependencyType? DependencyType { get; set; }

    public bool IsValid() => DependencyName is not null || DependencyType is not null;
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

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum DependabotPackageEcosystem
{
    Bundler,
    Cargo,
    Composer,
    Docker,
    Elixir,
    Elm,

    [EnumMember(Value = "gitsubmodule")]
    [YamlMember(Alias = "gitsubmodule")]
    GitSubmodule,

    [EnumMember(Value = "github-actions")]
    [YamlMember(Alias = "github-actions")]
    GithubActions,

    [EnumMember(Value = "gomod")]
    [YamlMember(Alias = "gomod")]
    GoModules,

    Gradle,
    Maven,
    Mix,
    Npm,
    NuGet,
    Pip,
    Terraform,
    Swift,
}

public enum DependabotScheduleInterval { Daily, Weekly, Monthly, }
public enum DependabotScheduleDay { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, }
public enum DependabotDependencyType { Direct, All, Production, Development, }
public enum DependabotRebaseStrategy { Disabled, Auto, }
public enum DependabotInsecureExternalCodeExecution { Allow, Deny, }

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum DependabotVersioningStrategy
{
    Auto,
    Widen,
    Increase,

    [EnumMember(Value = "lock-file-only")]
    [YamlMember(Alias = "lock-file-only")]
    LockFileOnly,

    [EnumMember(Value = "increase-if-necessary")]
    [YamlMember(Alias = "increase-if-necessary")]
    IncreaseIfNecessary,
}
