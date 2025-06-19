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
    private readonly Steam _steam;
    private readonly ILogger<ModList> _logger;
    private string? _modListPath;
    private string? _workshopPath;
    public string? LocalModsPath;
    private readonly List<string> _currentMods = [];
    private bool _modlistParsed;

    public ModList(Steam steam, ILogger<ModList> logger)
    {
        _steam = steam;
        _logger = logger;

        RefreshPaths();
        ParseModList();
    }

    private void RefreshPaths()
    {
        if (_steam.AppInstallDir == string.Empty) return;

        _modListPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Mods/modlist.txt"));
        _workshopPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "../../workshop/content/440900"));
        LocalModsPath = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Mods"));
    }

    private void ParseModList()
    {
        RefreshPaths();

        if (!File.Exists(_modListPath))
            return;

        _currentMods.Clear();

        string[] lines = File.ReadAllLines(_modListPath);
        for (int i = 0; i < lines.Length; i += 1)
        {
            string line = lines[i].Replace('\\', '/');
            string[] parts = line.Split('/');
            string modId = parts[^2];
            string pakName = parts[^1];
            _currentMods.Add($"{modId}/{pakName}");
        }

        _modlistParsed = true;

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

        for (int i = 0; i < mods.Length; i++)
        {
            _currentMods.Add(mods[i]);

            string modIdOrFolder = mods[i].Split('/')[0];
            if (ulong.TryParse(modIdOrFolder, out _))
            {
                mods[i] = Path.GetFullPath(Path.Combine(_workshopPath, mods[i]));
            }
            else
            {
                mods[i] = Path.GetFullPath(Path.Combine(LocalModsPath, mods[i]));
            }
        }

        File.WriteAllLines(_modListPath, mods, Encoding.UTF8);
        _logger.LogDebug("Modlist saved");
    }
}