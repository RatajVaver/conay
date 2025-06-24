using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class GameConfig
{
    private readonly ILogger<GameConfig> _logger;
    private readonly Steam _steam;

    private string? _defaultConfigPath;
    private string? _savedConfigPath;
    private string? _savedEnginePath;

    public GameConfig(Steam steam, ILogger<GameConfig> logger)
    {
        _logger = logger;
        _steam = steam;

        RefreshPaths();
    }

    private void RefreshPaths()
    {
        if (_steam.AppInstallDir == string.Empty) return;

        _defaultConfigPath =
            Path.GetFullPath(Path.Combine(_steam.AppInstallDir,
                "ConanSandbox/Config/DefaultGame.ini"));
        _savedConfigPath =
            Path.GetFullPath(Path.Combine(_steam.AppInstallDir,
                "ConanSandbox/Saved/Config/WindowsNoEditor/Game.ini"));
        _savedEnginePath =
            Path.GetFullPath(Path.Combine(_steam.AppInstallDir,
                "ConanSandbox/Saved/Config/WindowsNoEditor/Engine.ini"));
    }

    public bool ToggleCinematicIntro(bool disable = true)
    {
        RefreshPaths();
        if (_defaultConfigPath == null) return false;
        if (!File.Exists(_defaultConfigPath)) return false;

        try
        {
            string[] lines = File.ReadAllLines(_defaultConfigPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith($"{(disable ? '+' : '-')}StartupMovies=") && line.Length > 18)
                {
                    lines[i] = line.Replace(disable ? '+' : '-', disable ? '-' : '+');
                }
            }

            File.WriteAllLines(_defaultConfigPath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle cinematic intro (DefaultGame.ini)!");
        }

        return true;
    }

    public bool ToggleImmersiveMode(bool enable = true)
    {
        RefreshPaths();
        if (_savedConfigPath == null) return false;
        if (_savedEnginePath == null) return false;
        if (!File.Exists(_savedConfigPath)) return false;
        if (!File.Exists(_savedEnginePath)) return false;

        try
        {
            string[] lines = File.ReadAllLines(_savedConfigPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("VisibleSheathedWeapons="))
                {
                    lines[i] = "VisibleSheathedWeapons=" + (enable ? 2 : 0);
                }
                else if (line.StartsWith("showContextualControls="))
                {
                    lines[i] = "showContextualControls=" + (enable ? "False" : "True");
                }
                else if (line.StartsWith("ShowJourneyStepsUI="))
                {
                    lines[i] = "ShowJourneyStepsUI=" + (enable ? "False" : "True");
                }
            }

            File.WriteAllLines(_savedConfigPath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle immersive mode (Game.ini)!");
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(_savedEnginePath);

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

            File.WriteAllLines(_savedEnginePath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle immersive mode (Engine.ini)!");
            return false;
        }

        return true;
    }

    public bool SetLastConnected(string ip, string password)
    {
        RefreshPaths();
        if (_savedConfigPath == null) return false;
        if (!File.Exists(_savedConfigPath)) return false;

        try
        {
            string[] lines = File.ReadAllLines(_savedConfigPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("LastConnected=") && ip != "singleplayer")
                {
                    lines[i] = "LastConnected=" + ip;
                }
                else if (line.StartsWith("LastPassword=") && password != string.Empty)
                {
                    lines[i] = "LastPassword=" + password;
                }
                else if (line.StartsWith("StartedListenServerSession="))
                {
                    lines[i] = "StartedListenServerSession=" + (ip == "singleplayer" ? "True" : "False");
                }
            }

            File.WriteAllLines(_savedConfigPath, lines);
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
        RefreshPaths();
        if (_savedConfigPath == null || !File.Exists(_savedConfigPath)) return string.Empty;

        string ip = string.Empty;

        try
        {
            string[] lines = File.ReadAllLines(_savedConfigPath);
            foreach (string line in lines)
            {
                if (line.StartsWith("LastConnected="))
                {
                    ip = line.Replace("LastConnected=", "");
                }
                else if (line.StartsWith("StartedListenServerSession=True"))
                {
                    return "singleplayer";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last connected (Game.ini)!");
        }

        return ip;
    }
}