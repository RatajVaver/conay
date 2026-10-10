using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Conay.Data;
using Microsoft.Extensions.Logging;

namespace Conay.Utils;

[SupportedOSPlatform("linux")]
public static class Proton
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static bool Launch(ILogger logger, string steamInstallDir, string installDir, string exePath, string? args,
        bool battlEye)
    {
        if (!File.Exists(exePath))
        {
            logger.LogDebug("Proton launch not possible, game executable not found: {Exe}", exePath);
            return false;
        }

        if (FindSetup(steamInstallDir, installDir, logger) is not { } setup)
            return false;

        (string steamRoot, string steamApps, string compatData, string proton, string? runtimeDir,
            string[] libraries) = setup;

        string appId = GameVersionHelper.AppId.ToString();
        string protonDir = Path.GetDirectoryName(proton)!;

        List<string> command = [];

        if (runtimeDir is not null)
            command.AddRange([Path.Combine(runtimeDir, "_v2-entry-point"), "--verb=waitforexitandrun", "--"]);

        command.AddRange([proton, "waitforexitandrun", exePath]);
        if (!string.IsNullOrEmpty(args))
            command.AddRange(args.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        List<string> toolPaths = [protonDir];
        if (runtimeDir is not null)
            toolPaths.Add(runtimeDir);

        List<string> mounts = [.. toolPaths];
        string steamworksShared = Path.Combine(steamApps, "common/Steamworks Shared");
        if (Directory.Exists(steamworksShared))
            mounts.Insert(0, steamworksShared);

        string exeDir = Path.GetDirectoryName(exePath)!;
        if (!exeDir.StartsWith(installDir.TrimEnd('/') + "/", StringComparison.Ordinal))
            mounts.Add(exeDir);

        string shaderCache = Path.Combine(steamApps, "shadercache", appId);

        Dictionary<string, string> environment = new()
        {
            ["STEAM_COMPAT_APP_ID"] = appId,
            ["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamRoot,
            ["STEAM_COMPAT_DATA_PATH"] = compatData,
            ["STEAM_COMPAT_FLAGS"] = "search-cwd",
            ["STEAM_COMPAT_INSTALL_PATH"] = installDir,
            ["STEAM_COMPAT_LIBRARY_PATHS"] = steamApps,
            ["STEAM_COMPAT_MOUNTS"] = string.Join(':', mounts),
            ["STEAM_COMPAT_PROTON"] = "1",
            ["STEAM_COMPAT_TOOL_PATHS"] = string.Join(':', toolPaths),
            ["STEAM_COMPAT_SHADER_PATH"] = shaderCache,
            ["STEAM_COMPAT_TRANSCODED_MEDIA_PATH"] = shaderCache,
            ["STEAM_COMPAT_MEDIA_PATH"] = Path.Combine(shaderCache, "fozmediav1"),
            ["SteamAppId"] = appId,
            ["SteamGameId"] = appId,
            ["SteamOverlayGameId"] = appId
        };

        string[] overlay = new[] { "ubuntu12_32", "ubuntu12_64" }
            .Select(dir => Path.Combine(steamRoot, dir, "gameoverlayrenderer.so"))
            .Where(File.Exists)
            .ToArray();
        if (overlay.Length > 0)
        {
            environment["LD_PRELOAD"] = string.Join(':', overlay);
            environment["ENABLE_VK_LAYER_VALVE_steam_overlay_1"] = "1";
        }

        if (battlEye)
        {
            string? battlEyeRuntime = libraries
                .Select(lib => Path.Combine(lib, "common/Proton BattlEye Runtime"))
                .FirstOrDefault(Directory.Exists);

            if (battlEyeRuntime is not null)
            {
                environment["PROTON_BATTLEYE_RUNTIME"] = battlEyeRuntime;
                environment["STEAM_COMPAT_MOUNTS"] += ":" + battlEyeRuntime;
            }
            else
            {
                logger.LogWarning("Proton BattlEye Runtime is not installed, BattlEye will not work under Proton!");
            }
        }

        logger.LogDebug("Launching via Proton: {Command}", string.Join(' ', command));

        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "proton.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            return DetachedProcess.Start($"steam_app_{appId}", command, installDir, logPath, environment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch the game via Proton!");
            return false;
        }
    }

    public static bool CanLaunch(string steamInstallDir) => FindSetup(steamInstallDir, steamInstallDir) is not null;

    private sealed record Setup(
        string SteamRoot,
        string SteamApps,
        string CompatData,
        string Proton,
        string? RuntimeDir,
        string[] Libraries);

    private static Setup? FindSetup(string steamInstallDir, string installDir, ILogger? logger = null)
    {
        string appId = GameVersionHelper.AppId.ToString();
        string steamApps = Path.GetFullPath(Path.Combine(
            string.IsNullOrEmpty(steamInstallDir) ? installDir : steamInstallDir, "../.."));
        string compatData = Path.Combine(steamApps, "compatdata", appId);

        string? steamRoot = FindSteamRoot();
        string? proton = FindProton(compatData);
        if (steamRoot is null || proton is null)
        {
            logger?.LogDebug("Proton launch not possible (Steam: {Steam}, Proton: {Proton})",
                steamRoot ?? "not found", proton ?? "not found (game never started via Steam?)");
            return null;
        }

        string[] libraries = new[] { steamApps, Path.Combine(steamRoot, "steamapps") }.Distinct().ToArray();

        string? runtimeAppId = ReadVdfValue(Path.Combine(Path.GetDirectoryName(proton)!, "toolmanifest.vdf"),
            "require_tool_appid");
        string? runtimeDir = runtimeAppId is null ? null : FindApp(libraries, runtimeAppId);
        if (runtimeAppId is not null && runtimeDir is null)
        {
            logger?.LogDebug("Proton launch not possible, Steam Linux Runtime ({AppId}) is not installed",
                runtimeAppId);
            return null;
        }

        return new Setup(steamRoot, steamApps, compatData, proton, runtimeDir, libraries);
    }

    private static string? FindSteamRoot()
    {
        foreach (string dir in new[] { ".steam/root", ".local/share/Steam" }.Select(d => Path.Combine(Home, d)))
        {
            if (!Directory.Exists(Path.Combine(dir, "steamapps"))) continue;

            try
            {
                FileSystemInfo? target = Directory.ResolveLinkTarget(dir, returnFinalTarget: true);
                if (target is { Exists: true })
                    return target.FullName;
            }
            catch
            {
                // ignored
            }

            return dir;
        }

        return null;
    }

    private static string? FindProton(string compatData)
    {
        string configInfo = Path.Combine(compatData, "config_info");
        if (!File.Exists(configInfo)) return null;

        try
        {
            foreach (string line in File.ReadLines(configInfo))
            {
                int idx = line.IndexOf("/files/", StringComparison.Ordinal);
                if (idx < 0) idx = line.IndexOf("/dist/", StringComparison.Ordinal);
                if (idx <= 0) continue;

                string proton = Path.Combine(line[..idx], "proton");
                if (File.Exists(proton)) return proton;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string? FindApp(IEnumerable<string> libraries, string appId)
    {
        foreach (string library in libraries)
        {
            string? installDir = ReadVdfValue(Path.Combine(library, $"appmanifest_{appId}.acf"), "installdir");
            if (string.IsNullOrEmpty(installDir)) continue;

            string dir = Path.Combine(library, "common", installDir);
            if (Directory.Exists(dir)) return dir;
        }

        return null;
    }

    private static string? ReadVdfValue(string path, string key)
    {
        try
        {
            if (!File.Exists(path)) return null;
            Match match = Regex.Match(File.ReadAllText(path), $"\"{Regex.Escape(key)}\"\\s+\"([^\"]*)\"",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }
}