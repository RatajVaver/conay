using System.IO;

namespace Conay.Services;

public class GameConfig
{
    private readonly Steam _steam;

    private string? _defaultConfigPath;
    private string? _savedConfigPath;

    public GameConfig(Steam steam)
    {
        _steam = steam;
        RefreshPaths();
    }

    private void RefreshPaths()
    {
        if (_steam.AppInstallDir == string.Empty) return;

        _defaultConfigPath =
            Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Config/DefaultGame.ini"));
        _savedConfigPath =
            Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Saved/Config/WindowsNoEditor/Game.ini"));
    }

    public bool ToggleCinematicIntro(bool disable = true)
    {
        RefreshPaths();
        if (_defaultConfigPath == null) return false;
        if (!File.Exists(_defaultConfigPath)) return false;

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
        return true;
    }

    public bool SetLastConnected(string ip, string password)
    {
        RefreshPaths();
        if (_savedConfigPath == null) return false;
        if (!File.Exists(_savedConfigPath)) return false;

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
        return true;
    }

    public string GetLastConnected()
    {
        RefreshPaths();
        if (_savedConfigPath == null || !File.Exists(_savedConfigPath)) return string.Empty;

        string ip = string.Empty;

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

        return ip;
    }
}