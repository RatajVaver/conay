using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Conay.Data;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class GameConfig(Steam steam, ILogger<GameConfig> logger)
{
    private const string MoviePlayerSection = "[/Script/MoviePlayer.MoviePlayerSettings]";
    private const string StartupMoviesClearLine = "!StartupMovies=ClearArray";

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

    private bool ToggleCinematicIntroLegacy(bool disable)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Legacy);
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
            logger.LogError(ex, "Failed to toggle cinematic intro (DefaultGame.ini)!");
            return false;
        }

        return true;
    }

    private bool ToggleCinematicIntroEnhanced(bool disable)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Enhanced);
        if (string.IsNullOrEmpty(installDir)) return false;
        string path = Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Saved/Config/Windows/Game.ini"));
        if (!File.Exists(path)) return false;

        try
        {
            List<string> lines = File.ReadAllLines(path).ToList();
            int sectionIndex = lines.FindIndex(l => l.Trim() == MoviePlayerSection);

            if (sectionIndex == -1)
            {
                if (!disable) return true;

                if (lines.Count > 0 && lines[^1].Length > 0) lines.Add("");
                lines.Add(MoviePlayerSection);
                lines.Add(StartupMoviesClearLine);
            }
            else
            {
                int sectionEnd = lines.FindIndex(sectionIndex + 1, l => l.TrimStart().StartsWith('['));
                if (sectionEnd == -1) sectionEnd = lines.Count;

                int clearLineIndex = -1;
                for (int i = sectionIndex + 1; i < sectionEnd; i++)
                    if (lines[i].Trim() == StartupMoviesClearLine)
                        clearLineIndex = i;

                if (disable && clearLineIndex == -1)
                    lines.Insert(sectionIndex + 1, StartupMoviesClearLine);
                else if (!disable && clearLineIndex != -1)
                    lines.RemoveAt(clearLineIndex);
            }

            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle cinematic intro (Game.ini)!");
            return false;
        }

        return true;
    }

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
            logger.LogError(ex, "Failed to toggle immersive mode (Game.ini)!");
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
            logger.LogError(ex, "Failed to toggle immersive mode (Engine.ini)!");
            return false;
        }

        return true;
    }

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
            logger.LogError(ex, "Failed to set last connected (Game.ini)!");
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
            logger.LogError(ex, "Failed to get last connected (Game.ini)!");
        }

        return ip;
    }
}