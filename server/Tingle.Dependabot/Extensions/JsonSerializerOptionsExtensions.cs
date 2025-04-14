using System.Text.Json.Serialization;

namespace System.Text.Json;

/// <summary>Extension methods for <see cref="JsonSerializerOptions"/>.</summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>Setup standard options, used across all usages.</summary>
    public static JsonSerializerOptions UseStandard(this JsonSerializerOptions options)
    {
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals;
        options.WriteIndented = false;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.AllowTrailingCommas = true;
        options.ReadCommentHandling = JsonCommentHandling.Skip;

        options.TypeInfoResolverChain.Insert(0, Tingle.Dependabot.DependabotSerializerContext.Default);

        return options;
    }
}
