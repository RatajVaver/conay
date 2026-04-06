using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class ModList
{
    private readonly ILogger<ModList> _logger;
    private readonly Steam _steam;
    private readonly GameConfig _gameConfig;

    private string? _modListPath;
    private string? _serverModListPath;
    private string? _workshopPath;
    public string? LocalModsPath;
    private readonly List<string> _currentMods = [];
    private bool _modlistParsed;

    public ModList(ILogger<ModList> logger, Steam steam, GameConfig gameConfig)
    {
        _logger = logger;
        _steam = steam;
        _gameConfig = gameConfig;

        RefreshPaths();
        ParseModList();
    }

    private void RefreshPaths()
    {
        if (_steam.AppInstallDir == string.Empty) return;

        _modListPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Mods/modlist.txt"));
        _serverModListPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/servermodlist.txt"));
        _workshopPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "../../workshop/content/440900"));
        LocalModsPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Mods"));
    }

    private void LoadModListFromFile(string path)
    {
        _currentMods.Clear();

        try
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i += 1)
            {
                string line = lines[i].Replace('\\', '/');
                string[] parts = line.Split('/');
                if (parts.Length < 2) continue;

                string modId = parts[^2];
                string pakName = parts[^1];
                _currentMods.Add($"{modId}/{pakName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse current modlist!");
        }

        _modlistParsed = true;
    }

    private void ParseModList()
    {
        RefreshPaths();

        string? modListPath = GetNewerModlistFile();
        if (modListPath == null || !File.Exists(modListPath))
            return;

        LoadModListFromFile(modListPath);

        _logger.LogDebug("Currently loaded: {Mods} mods", _currentMods.Count);
    }

    public List<string> GetCurrentModList()
    {
        if (!_modlistParsed)
            ParseModList();

        return _currentMods;
    }

    public static DateTime GetModFileLastUpdate(string directoryPath, ulong modId)
    {
        string modDirectory = Path.Combine(directoryPath, modId.ToString());
        if (!Directory.Exists(modDirectory))
            return Epoch.UnixEpoch;

        string? pakName = new DirectoryInfo(modDirectory)
            .GetFiles().Select(x => x.Name)
            .FirstOrDefault(name => name.EndsWith(".pak"));
        if (pakName == null)
            return Epoch.UnixEpoch;

        string filePath = Path.Combine(modDirectory, pakName);
        if (!File.Exists(filePath))
            return Epoch.UnixEpoch;

        return File.GetLastWriteTimeUtc(filePath);
    }

    public DateTime GetLocalModFileLastUpdate(string directoryPath, string pakName)
    {
        RefreshPaths();

        if (LocalModsPath == null)
            return Epoch.ToDateTime(Epoch.Current);

        string filePath = Path.Combine(LocalModsPath, directoryPath, pakName + ".pak");
        return File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : Epoch.UnixEpoch;
    }

    public void SaveModList(string[] mods)
    {
        RefreshPaths();
        if (_workshopPath == null || _modListPath == null || LocalModsPath == null) return;

        _currentMods.Clear();

        string? modsDirectory = Path.GetDirectoryName(_modListPath);
        if (modsDirectory == null)
        {
            _logger.LogError("Can't resolve Mods directory!");
            return;
        }

        for (int i = 0; i < mods.Length; i++)
        {
            _currentMods.Add(mods[i]);

            string modIdOrFolder = mods[i].Split('/')[0];
            if (ulong.TryParse(modIdOrFolder, out _))
            {
                string fullPath = Path.GetFullPath(Path.Combine(_workshopPath, mods[i]));
                mods[i] = Path.GetRelativePath(modsDirectory, fullPath);
            }
            else
            {
                string fullPath = Path.GetFullPath(Path.Combine(LocalModsPath, mods[i]));
                mods[i] = Path.GetRelativePath(modsDirectory, fullPath);
            }
        }

        if (!Directory.Exists(modsDirectory))
        {
            try
            {
                Directory.CreateDirectory(modsDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Mods directory!");
                return;
            }
        }

        try
        {
            File.WriteAllLines(_modListPath, mods, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save modlist!");
            return;
        }

        _logger.LogDebug("Modlist saved");
    }

    private string? GetNewerModlistFile()
    {
        DateTime modListLastEdit = Epoch.UnixEpoch;
        if (_modListPath != null && File.Exists(_modListPath))
        {
            modListLastEdit = File.GetLastWriteTimeUtc(_modListPath);
        }

        if (_serverModListPath == null || !File.Exists(_serverModListPath))
            return _modListPath;

        DateTime serverModListLastEdit = File.GetLastWriteTimeUtc(_serverModListPath);
        if (serverModListLastEdit > modListLastEdit)
        {
            return _serverModListPath;
        }

        return _modListPath;
    }

    public List<string> ReloadCurrentModList()
    {
        _modlistParsed = false;
        ParseModList();
        return _currentMods;
    }
}