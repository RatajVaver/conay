using System.Text.Json.Serialization;

namespace Conay.Data;

public class PublishedFileDetails
{
    [JsonPropertyName("publishedfileid")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("file_size")]
    public string? Size { get; set; }

    [JsonPropertyName("time_updated")]
    public int? LastUpdate { get; set; }
}