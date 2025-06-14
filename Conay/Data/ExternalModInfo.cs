using System.Text.Json.Serialization;

namespace Conay.Data;

public class ExternalModInfo
{
    [JsonPropertyName("file")]
    public required string FileName { get; set; }

    [JsonPropertyName("updated")]
    public required int LastUpdate { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("authorUrl")]
    public string? AuthorUrl { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}