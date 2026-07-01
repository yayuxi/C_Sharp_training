using System.Text.Json.Serialization;

namespace ScraperTemplate.Helpers;

public class ExtractionPlan
{
    [JsonPropertyName("containerSelector")]
    public string ContainerSelector { get; set; } = "";

    [JsonPropertyName("fields")]
    public Dictionary<string, string> Fields { get; set; } = new();

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ContainerSelector) && Fields.Count > 0;
}