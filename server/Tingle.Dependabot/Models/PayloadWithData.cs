using System.ComponentModel.DataAnnotations;

namespace Tingle.Dependabot.Models;

public class PayloadWithData<T> where T : new()
{
    [Required]
    public T? Data { get; set; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
