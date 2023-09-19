using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEvent
{
    [Required]
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [Required]
    [JsonPropertyName("notificationId")]
    public int NotificationId { get; set; }

    [Required]
    [JsonPropertyName("eventType")]
    public AzureDevOpsEventType? EventType { get; set; }

    [Required]
    [JsonPropertyName("resource")]
    public JsonObject? Resource { get; set; }
}
