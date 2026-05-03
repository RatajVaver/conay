using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Conay.Data;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class GameConfig
{
    private readonly ILogger<GameConfig> _logger;
    private readonly Steam _steam;

    public GameConfig(Steam steam, ILogger<GameConfig> logger)
    {
        _logger = logger;
        _steam = steam;
    }

    public bool ToggleCinematicIntro(bool disable = true)
    {
        if (_steam.DualInstallMode)
        {
            bool s1 = ToggleCinematicIntroAt(disable, _steam.GetInstallDirForVersion(GameVersion.Enhanced));
            bool s2 = ToggleCinematicIntroAt(disable, _steam.GetInstallDirForVersion(GameVersion.Legacy));
            return s1 || s2;
        }
        return ToggleCinematicIntroAt(disable, _steam.AppInstallDir);
    }

    private bool ToggleCinematicIntroAt(bool disable, string installDir)
    {
        if (string.IsNullOrEmpty(installDir)) return false;
        string path = Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Config/DefaultGame.ini"));
        if (!File.Exists(path)) return false;

        try
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith($"{(disable ? '+' : '-')}StartupMovies=") && line.Length > 18)
                    lines[i] = line.Replace(disable ? '+' : '-', disable ? '-' : '+');
            }
            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle cinematic intro (DefaultGame.ini)!");
        }

        return true;
    }

    public bool ToggleImmersiveMode(bool enable = true)
    {
        if (_steam.DualInstallMode)
        {
            bool s1 = ToggleImmersiveModeAt(enable, _steam.GetInstallDirForVersion(GameVersion.Enhanced));
            bool s2 = ToggleImmersiveModeAt(enable, _steam.GetInstallDirForVersion(GameVersion.Legacy));
            return s1 || s2;
        }
        return ToggleImmersiveModeAt(enable, _steam.AppInstallDir);
    }

    private bool ToggleImmersiveModeAt(bool enable, string installDir)
    {
        if (string.IsNullOrEmpty(installDir)) return false;
        string gamePath = Path.GetFullPath(Path.Combine(installDir,
            "ConanSandbox/Saved/Config/WindowsNoEditor/Game.ini"));
        string enginePath = Path.GetFullPath(Path.Combine(installDir,
            "ConanSandbox/Saved/Config/WindowsNoEditor/Engine.ini"));

        if (!File.Exists(gamePath)) return false;
        if (!File.Exists(enginePath)) return false;

        try
        {
            string[] lines = File.ReadAllLines(gamePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("VisibleSheathedWeapons="))
                    lines[i] = "VisibleSheathedWeapons=" + (enable ? 2 : 0);
                else if (line.StartsWith("showContextualControls="))
                    lines[i] = "showContextualControls=" + (enable ? "False" : "True");
                else if (line.StartsWith("ShowJourneyStepsUI="))
                    lines[i] = "ShowJourneyStepsUI=" + (enable ? "False" : "True");
            }
            File.WriteAllLines(gamePath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle immersive mode (Game.ini)!");
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(enginePath);
            bool lineFound = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("UnfocusedVolumeMultiplier="))
                {
                    lines[i] = "UnfocusedVolumeMultiplier=" + (enable ? "0.25" : "0.0");
                    lineFound = true;
                }
            }

            if (!lineFound && enable)
            {
                List<string> temp = lines.ToList();
                temp.Add("");
                temp.Add("[Audio]");
                temp.Add("UnfocusedVolumeMultiplier=0.25");
                lines = temp.ToArray();
            }

            File.WriteAllLines(enginePath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle immersive mode (Engine.ini)!");
            return false;
        }

        return true;
    }

    public bool SetLastConnected(string ip, string password, string? installDir = null)
    {
        string dir = installDir ?? _steam.AppInstallDir;
        if (string.IsNullOrEmpty(dir)) return false;
        string savedConfigPath = Path.GetFullPath(Path.Combine(dir,
            "ConanSandbox/Saved/Config/WindowsNoEditor/Game.ini"));
        if (!File.Exists(savedConfigPath)) return false;

        try
        {
            string[] lines = File.ReadAllLines(savedConfigPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("LastConnected=") && ip != "singleplayer")
                    lines[i] = "LastConnected=" + ip;
                else if (line.StartsWith("LastPassword=") && password != string.Empty)
                    lines[i] = "LastPassword=" + password;
                else if (line.StartsWith("StartedListenServerSession="))
                    lines[i] = "StartedListenServerSession=" + (ip == "singleplayer" ? "True" : "False");
            }
            File.WriteAllLines(savedConfigPath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set last connected (Game.ini)!");
            return false;
        }

        return true;
    }

    public string GetLastConnected()
    {
        if (string.IsNullOrEmpty(_steam.AppInstallDir)) return string.Empty;
        string savedConfigPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir,
            "ConanSandbox/Saved/Config/WindowsNoEditor/Game.ini"));
        if (!File.Exists(savedConfigPath)) return string.Empty;

        string ip = string.Empty;
        try
        {
            string[] lines = File.ReadAllLines(savedConfigPath);
            foreach (string line in lines)
            {
                if (line.StartsWith("LastConnected="))
                    ip = line.Replace("LastConnected=", "");
                else if (line.StartsWith("StartedListenServerSession=True"))
                    return "singleplayer";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last connected (Game.ini)!");
        }

        return ip;
    }
}
