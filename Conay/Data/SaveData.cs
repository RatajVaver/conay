using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Conay.Data;

public class SaveData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("modlist")]
    public List<string> Modlist { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastPlayedAt")]
    public DateTime? LastPlayedAt { get; set; }
}
