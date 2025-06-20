using System.Text.Json.Serialization;
using Conay.Services;

namespace Conay.Data;

public class ServerInfo
{
    [JsonPropertyName("file")]
    public required string File { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("players")]
    public int? Players { get; set; }

    [JsonPropertyName("maxplayers")]
    public int? MaxPlayers { get; set; }

    [JsonPropertyName("map")]
    public string? Map { get; set; }

    [JsonIgnore]
    public IPresetService? Provider { get; set; }
}