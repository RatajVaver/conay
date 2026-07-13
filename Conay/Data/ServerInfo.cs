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

    [JsonPropertyName("maxPlayers")]
    public int? MaxPlayers { get; set; }

    [JsonPropertyName("map")]
    public string? Map { get; set; }

    [JsonPropertyName("ip")]
    public required string Ip { get; set; }

    [JsonPropertyName("query")]
    public int? QueryPort { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("discord")]
    public string? Discord { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("mods")]
    public int ModsCount { get; set; }

    [JsonIgnore]
    public IPresetService? Provider { get; set; }
}