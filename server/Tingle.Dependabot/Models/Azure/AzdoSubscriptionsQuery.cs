using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoSubscriptionsQuery
{
    [JsonPropertyName("consumerActionId")]
    public string? ConsumerActionId { get; set; }

    [JsonPropertyName("consumerId")]
    public string? ConsumerId { get; set; }

    [JsonPropertyName("consumerInputFilters")]
    public List<AzdoSubscriptionsQueryInputFilter>? ConsumerInputFilters { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("publisherId")]
    public string? PublisherId { get; set; }

    [JsonPropertyName("publisherInputFilters")]
    public List<AzdoSubscriptionsQueryInputFilter>? PublisherInputFilters { get; set; }

    [JsonPropertyName("subscriberId")]
    public string? SubscriberId { get; set; }
}

public class AzdoSubscriptionsQueryInputFilter
{
    [JsonPropertyName("conditions")]
    public List<AzdoSubscriptionsQueryInputFilterCondition>? Conditions { get; set; }
}

public class AzdoSubscriptionsQueryInputFilterCondition
{
    [JsonPropertyName("caseSensitive")]
    public bool? CaseSensitive { get; set; }

    [JsonPropertyName("inputId")]
    public string? InputId { get; set; }

    [JsonPropertyName("inputValue")]
    public string? InputValue { get; set; }

    [JsonPropertyName("operator")]
    public AzdoSubscriptionsQueryInputFilterOperator Operator { get; set; }
}

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum AzdoSubscriptionsQueryInputFilterOperator
{
    [EnumMember(Value = "equals")]
    Equals,
    [EnumMember(Value = "notEquals")]
    NotEquals
}

public class AzdoSubscriptionsQueryResponse : AzdoSubscriptionsQuery
{
    [JsonPropertyName("results")]
    public required List<AzdoSubscription> Results { get; set; }
}

public class AzdoSubscription
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public AzdoSubscriptionStatus Status { get; set; }

    [JsonPropertyName("publisherId")]
    public required string PublisherId { get; set; }

    [JsonPropertyName("publisherInputs")]
    public Dictionary<string, string> PublisherInputs { get; set; } = new();

    [JsonPropertyName("consumerId")]
    public string? ConsumerId { get; set; }

    [JsonPropertyName("consumerActionId")]
    public string? ConsumerActionId { get; set; }

    [JsonPropertyName("consumerInputs")]
    public Dictionary<string, string> ConsumerInputs { get; set; } = new();

    [JsonPropertyName("eventType")]
    public required string EventType { get; set; }

    [JsonPropertyName("resourceVersion")]
    public required string ResourceVersion { get; set; }

    [JsonPropertyName("eventDescription")]
    public string? EventDescription { get; set; }

    [JsonPropertyName("actionDescription")]
    public string? ActionDescription { get; set; }
}

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum AzdoSubscriptionStatus
{
    [EnumMember(Value = "enabled")]
    Enabled,

    [EnumMember(Value = "onProbation")]
    OnProbation,

    [EnumMember(Value = "disabledByUser")]
    DisabledByUser,

    [EnumMember(Value = "disabledBySystem")]
    DisabledBySystem,

    [EnumMember(Value = "disabledByInactiveIdentity")]
    DisabledByInactiveIdentity,
}
