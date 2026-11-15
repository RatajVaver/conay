using System;
using System.Collections.Generic;
using System.IO;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class GameConfig(Steam steam, ILogger<GameConfig> logger)
{
    private static readonly string[] CinematicMovieNames =
    [
        "Exiles Cinematic Intro",
        "Funcom Splash",
        "Inflexion_Logo",
        "Inflexion_Logo_1080",
        "Intro_Cinematic_Siptah",
        "Nvidia_Splash",
        "Splash_Inflexion",
        "StartupNvidia",
        "StartupUnreal"
    ];

    private static string SavedConfigFolder(GameVersion version) =>
        version == GameVersion.Enhanced ? "Windows" : "WindowsNoEditor";

    public bool ToggleCinematicIntro(bool disable = true)
    {
        if (!steam.DualInstallMode)
            return GameVersionHelper.Current == GameVersion.Legacy
                ? ToggleCinematicIntroLegacy(disable)
                : ToggleCinematicIntroEnhanced(disable);
        bool s1 = ToggleCinematicIntroEnhanced(disable);
        bool s2 = ToggleCinematicIntroLegacy(disable);
        return s1 || s2;
    }

    private const string MoviePlayerSection = "[/Script/MoviePlayer.MoviePlayerSettings]";

    private bool ToggleCinematicIntroLegacy(bool disable)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Legacy);
        if (string.IsNullOrEmpty(installDir)) return false;
        string path = Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Config/DefaultGame.ini"));
        if (!File.Exists(path)) return false;

        try
        {
            IniFile ini = IniFile.Load(path);
            ini.SwapArrayOp(MoviePlayerSection, "StartupMovies", disable ? '+' : '-', disable ? '-' : '+');
            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle cinematic intro (DefaultGame.ini)!");
            return false;
        }

        return true;
    }

    private bool ToggleCinematicIntroEnhanced(bool disable)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Enhanced);
        if (string.IsNullOrEmpty(installDir)) return false;
        string moviesDir = Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Content/Movies"));
        if (!Directory.Exists(moviesDir)) return false;

        bool anyFailed = false;
        foreach (string name in CinematicMovieNames)
        {
            string activePath = Path.Combine(moviesDir, name + ".bk2");
            string disabledPath = Path.Combine(moviesDir, name + ".bk2.bak");

            try
            {
                if (disable)
                {
                    if (File.Exists(activePath) && !File.Exists(disabledPath))
                        File.Move(activePath, disabledPath);
                }
                else
                {
                    if (File.Exists(disabledPath) && !File.Exists(activePath))
                        File.Move(disabledPath, activePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to toggle cinematic intro movie '{Name}'!", name);
                anyFailed = true;
            }
        }

        return !anyFailed;
    }

    private const string GameplaySection = "[Settings.Gameplay]";
    private const string AudioSection = "[Audio]";
    private const string UnfocusedVolumeKey = "UnfocusedVolumeMultiplier";

    public bool ToggleImmersiveMode(bool enable = true)
    {
        if (!steam.DualInstallMode)
            return ToggleImmersiveModeAt(enable, GameVersionHelper.Current, steam.AppInstallDir);
        bool s1 = ToggleImmersiveModeAt(enable, GameVersion.Enhanced,
            steam.GetInstallDirForVersion(GameVersion.Enhanced));
        bool s2 = ToggleImmersiveModeAt(enable, GameVersion.Legacy,
            steam.GetInstallDirForVersion(GameVersion.Legacy));
        return s1 || s2;
    }

    private bool ToggleImmersiveModeAt(bool enable, GameVersion version, string installDir)
    {
        if (string.IsNullOrEmpty(installDir)) return false;
        string folder = SavedConfigFolder(version);
        string gamePath = Path.GetFullPath(Path.Combine(installDir,
            $"ConanSandbox/Saved/Config/{folder}/Game.ini"));
        string enginePath = Path.GetFullPath(Path.Combine(installDir,
            $"ConanSandbox/Saved/Config/{folder}/Engine.ini"));

        if (!File.Exists(gamePath)) return false;
        if (!File.Exists(enginePath)) return false;

        try
        {
            IniFile ini = IniFile.Load(gamePath);
            ini.Set(GameplaySection, "VisibleSheathedWeapons", enable ? "2" : "0");
            ini.Set(GameplaySection, "showContextualControls", enable ? "False" : "True");
            ini.Set(GameplaySection, "ShowJourneyStepsUI", enable ? "False" : "True");
            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle immersive mode (Game.ini)!");
            return false;
        }

        try
        {
            IniFile ini = IniFile.Load(enginePath);
            if (enable || ini.Get(AudioSection, UnfocusedVolumeKey) != null)
                ini.Set(AudioSection, UnfocusedVolumeKey, enable ? "0.25" : "0.0");
            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle immersive mode (Engine.ini)!");
            return false;
        }

        return true;
    }

    private const string SavedServersSection = "[SavedServers]";
    private const string SavedCoopDataSection = "[SavedCoopData]";

    public bool SetLastConnected(string ip, string password, GameVersion? version = null)
    {
        GameVersion v = version ?? GameVersionHelper.Current;
        string dir = steam.GetInstallDirForVersion(v);
        if (string.IsNullOrEmpty(dir)) return false;
        string savedConfigPath = Path.GetFullPath(Path.Combine(dir,
            $"ConanSandbox/Saved/Config/{SavedConfigFolder(v)}/Game.ini"));
        if (!File.Exists(savedConfigPath)) return false;

        try
        {
            IniFile ini = IniFile.Load(savedConfigPath);
            if (ip != "singleplayer")
                ini.Set(SavedServersSection, "LastConnected", ip);
            if (password != string.Empty)
                ini.Set(SavedServersSection, "LastPassword", password);
            ini.Set(SavedCoopDataSection, "StartedListenServerSession", ip == "singleplayer" ? "True" : "False");
            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set last connected (Game.ini)!");
            return false;
        }

        return true;
    }

    public bool RemoveServerModListEntry()
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Enhanced);
        if (string.IsNullOrEmpty(installDir)) return false;
        string path =
            Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Saved/Config/Windows/ServerSettings.ini"));
        if (!File.Exists(path)) return false;

        try
        {
            IniFile ini = IniFile.Load(path);
            ini.Remove("[ServerSettings]", "ServerModList");
            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove ServerModList entry (ServerSettings.ini)!");
            return false;
        }

        return true;
    }

    private const string ConaySyncSection =
        "[/Game/Mods/ConaySync/PreLoad/W_ConaySync.W_ConaySync_C]";

    public bool WriteConaySyncData(string name, string icon, string ip, string password)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Enhanced);
        if (string.IsNullOrEmpty(installDir)) return false;
        string enginePath = Path.GetFullPath(Path.Combine(installDir,
            "ConanSandbox/Saved/Config/Windows/Engine.ini"));
        if (!File.Exists(enginePath)) return false;

        Dictionary<string, string> values = new()
        {
            ["Name"] = name,
            ["IconURL"] = icon,
            ["IP"] = ip,
            ["Password"] = password,
            ["Updated"] = Epoch.Current.ToString()
        };

        try
        {
            IniFile ini = IniFile.Load(enginePath);
            ini.Set(ConaySyncSection, values);
            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write ConaySync data (Engine.ini)!");
            return false;
        }

        return true;
    }

    private const string SystemSettingsSection = "[/Script/ConanSandbox.SystemSettings]";
    private const string MountModsInPlaceKey = "dw.MountModsInPlace";

    public bool SetMountModsInPlace(bool enable)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Enhanced);
        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(Path.Combine(installDir, "ConanSandbox")))
            return false;
        string enginePath = Path.GetFullPath(Path.Combine(installDir,
            "ConanSandbox/Saved/Config/Windows/Engine.ini"));

        try
        {
            IniFile ini = IniFile.Load(enginePath);
            if (enable)
            {
                ini.Set(SystemSettingsSection, MountModsInPlaceKey, "1");
            }
            else
            {
                ini.Remove(SystemSettingsSection, MountModsInPlaceKey);
            }

            ini.Save();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set {Key} (Engine.ini)!", MountModsInPlaceKey);
            return false;
        }

        return true;
    }

    public string GetLastConnected(GameVersion? version = null)
    {
        GameVersion v = version ?? GameVersionHelper.Current;
        string installDir = steam.GetInstallDirForVersion(v);
        if (string.IsNullOrEmpty(installDir)) return string.Empty;
        string savedConfigPath = Path.GetFullPath(Path.Combine(installDir,
            $"ConanSandbox/Saved/Config/{SavedConfigFolder(v)}/Game.ini"));
        if (!File.Exists(savedConfigPath)) return string.Empty;

        try
        {
            IniFile ini = IniFile.Load(savedConfigPath);
            string? listen = ini.Get(SavedCoopDataSection, "StartedListenServerSession");
            if (string.Equals(listen?.Trim(), "True", StringComparison.OrdinalIgnoreCase))
                return "singleplayer";
            return ini.Get(SavedServersSection, "LastConnected") ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get last connected (Game.ini)!");
        }

        return string.Empty;
    }
}