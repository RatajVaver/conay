using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class ModList(ILogger<ModList> logger, Steam steam)
{
    public string? WorkshopPath => steam.AppInstallDir == string.Empty
        ? null
        : Path.GetFullPath(Path.Combine(steam.AppInstallDir, "../../workshop/content/440900"));

    public string? LocalModsPath => steam.AppInstallDir == string.Empty
        ? null
        : Path.GetFullPath(Path.Combine(steam.AppInstallDir, "ConanSandbox/Mods"));

    private readonly List<string> _currentMods = [];

    private string GetModListPath(GameVersion version) =>
        Path.GetFullPath(Path.Combine(steam.GetInstallDirForVersion(version), "ConanSandbox/Mods/modlist.txt"));

    private string GetServerModListPath(GameVersion version) =>
        Path.GetFullPath(Path.Combine(steam.GetInstallDirForVersion(version), "ConanSandbox/servermodlist.txt"));

    private string GetModRestartDataPath(GameVersion version) =>
        Path.GetFullPath(Path.Combine(steam.GetInstallDirForVersion(version), "ConanSandbox/Saved/ModRestartData.json"));


    private void LoadModListFromFile(string path)
    {
        _currentMods.Clear();

        try
        {
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                string[] parts = line.Replace('\\', '/').Split('/');
                if (parts.Length < 2) continue;

                string modId = parts[^2];
                string pakName = parts[^1];
                _currentMods.Add($"{modId}/{pakName}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse current modlist!");
        }
    }

    public List<string> GetCurrentModList() => _currentMods;

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
        if (LocalModsPath == null)
            return Epoch.ToDateTime(Epoch.Current);

        string filePath = Path.Combine(LocalModsPath, directoryPath, pakName + ".pak");
        return File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : Epoch.UnixEpoch;
    }

    public void SaveModList(string[] mods, GameVersion? version = null)
    {
        if (WorkshopPath == null) return;

        GameVersion ver = version ?? GameVersionHelper.Current;
        string installDir = steam.GetInstallDirForVersion(ver);
        string modListPath = GetModListPath(ver);
        string localModsPath = Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Mods"));
        string? modsDirectory = Path.GetDirectoryName(modListPath);
        if (modsDirectory == null)
        {
            logger.LogError("Can't resolve Mods directory!");
            return;
        }

        _currentMods.Clear();
        string[] resolvedMods = new string[mods.Length];
        for (int i = 0; i < mods.Length; i++)
        {
            _currentMods.Add(mods[i]);
            string modIdOrFolder = mods[i].Split('/')[0];
            string fullPath = ulong.TryParse(modIdOrFolder, out _)
                ? Path.GetFullPath(Path.Combine(WorkshopPath, mods[i]))
                : Path.GetFullPath(Path.Combine(localModsPath, mods[i]));
            resolvedMods[i] = Path.GetRelativePath(modsDirectory, fullPath);
        }

        if (!Directory.Exists(modsDirectory))
        {
            try
            {
                Directory.CreateDirectory(modsDirectory);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Mods directory!");
                return;
            }
        }

        try
        {
            File.WriteAllLines(modListPath, resolvedMods, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save modlist!");
            return;
        }

        DeleteIfExists(GetServerModListPath(ver), "servermodlist.txt");
        DeleteIfExists(GetModRestartDataPath(ver), "ModRestartData.json");

        logger.LogDebug("Modlist saved");
    }

    private void DeleteIfExists(string path, string name)
    {
        if (!File.Exists(path)) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove {Name}!", name);
        }
    }

    public void LoadModList(GameVersion version)
    {
        string modListPath = GetModListPath(version);
        string serverModListPath = GetServerModListPath(version);

        bool hasModList = File.Exists(modListPath);
        bool hasServerModList = File.Exists(serverModListPath);

        if (!hasModList && !hasServerModList) return;

        string pathToLoad = hasModList && hasServerModList
            ? (File.GetLastWriteTimeUtc(serverModListPath) > File.GetLastWriteTimeUtc(modListPath)
                ? serverModListPath
                : modListPath)
            : (hasServerModList ? serverModListPath : modListPath);

        LoadModListFromFile(pathToLoad);
        logger.LogDebug("Loaded {Count} mods from {Path}", _currentMods.Count, pathToLoad);
    }

    public List<string> ReloadCurrentModListForVersion(GameVersion version)
    {
        LoadModList(steam.DualInstallMode ? version : GameVersionHelper.Current);
        return _currentMods;
    }
}