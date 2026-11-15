using System.Diagnostics;
using Conay.Utils;

namespace Conay.Tests.Utils;

// The environment processes Conay starts get, other than the game
public class StartupEnvironmentTests
{
    // What connecting to Steam as Conan Exiles put into Conay's environment, seen in dumps of a Steam Conay started
    internal static readonly Dictionary<string, string> ConanExilesGameEnvironment = new()
    {
        ["AMD_VK_PIPELINE_CACHE_FILENAME"] = "steamapp_shader_cache",
        ["AMD_VK_PIPELINE_CACHE_PATH"] = @"X:\SteamLibrary\steamapps\shadercache\440900\AMDv1",
        ["AMD_VK_USE_PIPELINE_CACHE"] = "1",
        ["DXVK_STATE_CACHE_PATH"] = @"X:\SteamLibrary\steamapps\shadercache\440900\DXVK_state_cache",
        ["ENABLE_VK_LAYER_VALVE_steam_fossilize_1"] = "1",
        ["ENABLE_VK_LAYER_VALVE_steam_overlay_1"] = "1",
        ["MESA_DISK_CACHE_READ_ONLY_FOZ_DBS"] = "steam_cache,steam_precompiled",
        ["MESA_DISK_CACHE_SINGLE_FILE"] = "1",
        ["MESA_GLSL_CACHE_DIR"] = @"X:\SteamLibrary\steamapps\shadercache\440900",
        ["MESA_GLSL_CACHE_MAX_SIZE"] = "5G",
        ["MESA_SHADER_CACHE_DIR"] = @"X:\SteamLibrary\steamapps\shadercache\440900",
        ["MESA_SHADER_CACHE_MAX_SIZE"] = "5G"
    };

    internal static readonly Dictionary<string, string> Startup = new()
    {
        ["PATH"] = @"C:\Windows\system32;C:\Windows",
        ["USERPROFILE"] = @"C:\Users\alice",
        ["MESA_LOADER_DRIVER_OVERRIDE"] = "radeonsi"
    };

    [Fact]
    public void Apply_LeavesOutWhatConnectingToSteamAdded()
    {
        Dictionary<string, string?> environment = WithGameEnvironment(Startup);

        StartupEnvironment.Apply(environment, Startup);

        Assert.Equal(Startup.OrderBy(v => v.Key), environment.OrderBy(v => v.Key)!);
    }

    [Fact]
    public void Apply_LeavesOutAnythingElseAddedAfterStartup()
    {
        Dictionary<string, string?> environment = new(Startup!)
        {
            ["__GL_SHADER_DISK_CACHE_PATH"] = @"X:\SteamLibrary\steamapps\shadercache\440900\nvidiav1"
        };

        StartupEnvironment.Apply(environment, Startup);

        Assert.DoesNotContain("__GL_SHADER_DISK_CACHE_PATH", environment.Keys);
    }

    [Fact]
    public void Apply_RestoresStartupValues()
    {
        Dictionary<string, string?> environment = new(Startup!) { ["PATH"] = @"C:\Steam;C:\Windows" };

        StartupEnvironment.Apply(environment, Startup);

        Assert.Equal(Startup["PATH"], environment["PATH"]);
    }

    [Fact]
    public void Apply_LeavesOutSteamsGameSettingsConayWasStartedWith()
    {
        Dictionary<string, string> startup = WithGameEnvironment(Startup)
            .Concat(new Dictionary<string, string?> { ["SteamAppId"] = "440900", ["SteamGameId"] = "440900" })
            .ToDictionary(v => v.Key, v => v.Value!);
        Dictionary<string, string?> environment = new();

        StartupEnvironment.Apply(environment, startup);

        Assert.Equal(Startup.OrderBy(v => v.Key), environment.OrderBy(v => v.Key)!);
    }

    [Fact]
    public void Apply_ReplacesWhatAProcessWouldInheritFromConay()
    {
        ProcessStartInfo startInfo = new("steam.exe");

        StartupEnvironment.Apply(startInfo.Environment, Startup);

        Assert.Equal(Startup.Keys.Order(StringComparer.OrdinalIgnoreCase),
            startInfo.Environment.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("SteamAppId")]
    [InlineData("SteamGameId")]
    [InlineData("SteamOverlayGameId")]
    [InlineData("steamappid")]
    [InlineData("ENABLE_VK_LAYER_VALVE_steam_overlay_1")]
    [InlineData("ENABLE_VK_LAYER_VALVE_steam_fossilize_1")]
    [InlineData("AMD_VK_PIPELINE_CACHE_PATH")]
    [InlineData("MESA_SHADER_CACHE_DIR")]
    public void IsSteamGameVariable_RecognisesSteamsGameSettings(string name)
    {
        Assert.True(StartupEnvironment.IsSteamGameVariable(name));
    }

    [Theory]
    [InlineData("PATH")]
    [InlineData("MESA_LOADER_DRIVER_OVERRIDE")]
    [InlineData("ENABLE_VK_LAYER_NV_optimus")]
    public void IsSteamGameVariable_LeavesOtherVariablesAlone(string name)
    {
        Assert.False(StartupEnvironment.IsSteamGameVariable(name));
    }

    [Fact]
    public void EveryVariableConnectingAdded_IsASteamGameVariable()
    {
        Assert.All(ConanExilesGameEnvironment.Keys, name => Assert.True(StartupEnvironment.IsSteamGameVariable(name)));
    }

    private static Dictionary<string, string?> WithGameEnvironment(Dictionary<string, string> environment)
    {
        Dictionary<string, string?> combined = new(environment!);
        foreach ((string name, string value) in ConanExilesGameEnvironment)
            combined[name] = value;

        return combined;
    }
}
