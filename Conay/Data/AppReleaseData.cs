using System.Text.Json.Serialization;

namespace Conay.Data;

public class AppReleaseData
{
    [JsonPropertyName("tag_name")]
    public required string Version { get; set; }
}