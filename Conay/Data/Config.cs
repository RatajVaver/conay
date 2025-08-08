using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Conay.Data;

public class Config
{
    [JsonPropertyName("checkUpdates")]
    public bool CheckUpdates { get; set; } = true;

    [JsonPropertyName("updateSubscribed")]
    public bool UpdateSubscribedModsOnLaunch { get; set; } = true;

    [JsonPropertyName("autoSubscribe")]
    public bool AutomaticallySubscribe { get; set; }

    [JsonPropertyName("launch")]
    public bool LaunchGame { get; set; } = true;

    [JsonPropertyName("direct")]
    public bool DirectConnect { get; set; } = true;

    [JsonPropertyName("disableCinematic")]
    public bool DisableCinematic { get; set; }

    [JsonPropertyName("immersiveMode")]
    public bool ImmersiveMode { get; set; }

    [JsonPropertyName("offline")]
    public bool OfflineMode { get; set; }

    [JsonPropertyName("clipboard")]
    public bool Clipboard { get; set; } = true;

    [JsonPropertyName("menuCollapsed")]
    public bool MenuCollapsed { get; set; }

    [JsonPropertyName("displayIcons")]
    public bool DisplayIcons { get; set; } = true;

    [JsonPropertyName("cache")]
    public bool UseCache { get; set; } = true;

    [JsonPropertyName("defaultTab")]
    public string DefaultTab { get; set; } = "servers";

    [JsonPropertyName("queryServers")]
    public bool QueryServers { get; set; } = true;

    [JsonPropertyName("keepHistory")]
    public bool KeepHistory { get; set; } = true;

    [JsonPropertyName("history")]
    public List<string> History { get; set; } = [];

    [JsonPropertyName("favorites")]
    public List<string> Favorites { get; set; } = [];
}