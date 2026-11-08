using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Conay.Utils;

// The environment for processes Conay starts other than the game. Connecting to Steam as Conan Exiles adds the
// game's settings to Conay's own environment (shader cache paths, Steam's Vulkan layers), which are meant for the
// game only.
public static class StartupEnvironment
{
    // What Steam sets up for a running game, seen in Conay's environment after connecting
    private static readonly HashSet<string> SteamGameVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SteamAppId", "SteamGameId", "SteamOverlayGameId",
        "AMD_VK_PIPELINE_CACHE_FILENAME", "AMD_VK_PIPELINE_CACHE_PATH", "AMD_VK_USE_PIPELINE_CACHE",
        "DXVK_STATE_CACHE_PATH",
        "MESA_DISK_CACHE_READ_ONLY_FOZ_DBS", "MESA_DISK_CACHE_SINGLE_FILE", "MESA_GLSL_CACHE_DIR",
        "MESA_GLSL_CACHE_MAX_SIZE", "MESA_SHADER_CACHE_DIR", "MESA_SHADER_CACHE_MAX_SIZE"
    };

    private const string SteamVulkanLayerPrefix = "ENABLE_VK_LAYER_VALVE_";

    private static IReadOnlyDictionary<string, string>? _variables;

    // Has to run before anything connects to Steam
    public static void Capture() => _variables ??= Read();

    public static IReadOnlyDictionary<string, string> Variables => _variables ??= Read();

    public static void ApplyTo(ProcessStartInfo startInfo) => Apply(startInfo.Environment, Variables);

    // Replaces the environment with the startup one, leaving Steam's game settings out in case Conay was started
    // with them (by Steam, or by a Conay that was connected to it)
    internal static void Apply(IDictionary<string, string?> environment, IReadOnlyDictionary<string, string> startup)
    {
        environment.Clear();
        foreach ((string key, string value) in startup.Where(v => !IsSteamGameVariable(v.Key)))
            environment[key] = value;
    }

    internal static bool IsSteamGameVariable(string name) =>
        SteamGameVariables.Contains(name) || name.StartsWith(SteamVulkanLayerPrefix, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> Read()
    {
        Dictionary<string, string> variables = new(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            variables[(string)entry.Key] = entry.Value as string ?? string.Empty;

        return variables;
    }
}
