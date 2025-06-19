using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Conay.Data;

public class ServerData
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("ip")]
    public required string Ip { get; set; }

    [JsonPropertyName("query")]
    public int? QueryPort { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("discord")]
    public string? Discord { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("mods")]
    public List<string> Mods { get; set; } = [];

    [JsonIgnore]
    public string? FileName { get; set; }
}