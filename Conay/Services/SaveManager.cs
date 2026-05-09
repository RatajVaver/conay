using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Conay.Data;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class SaveManager(ILogger<SaveManager> logger, Steam steam)
{
    private const string MarkerFileName = "conay_current.txt";
    private const string SaveMetaFileName = "save.json";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static string SavesDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "saves"));

    private string GameSavedDir => Path.GetFullPath(Path.Combine(
        steam.DualInstallMode ? steam.GetInstallDirForVersion(GameVersion.Legacy) : steam.AppInstallDir,
        "ConanSandbox/Saved"));

    private string MarkerFilePath => Path.Combine(GameSavedDir, MarkerFileName);

    public List<FileInfo> GetActiveDbFiles()
    {
        if (!Directory.Exists(GameSavedDir)) return [];

        return [..new DirectoryInfo(GameSavedDir).GetFiles("*.db")];
    }

    public string? GetCurrentSaveSlug()
    {
        if (!File.Exists(MarkerFilePath)) return null;
        string slug = File.ReadAllText(MarkerFilePath).Trim();
        return string.IsNullOrEmpty(slug) ? null : slug;
    }

    public SaveData? GetSaveData(string slug)
    {
        string metaPath = Path.Combine(SavesDir, slug, SaveMetaFileName);
        if (!File.Exists(metaPath)) return null;

        try
        {
            return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(metaPath));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read save data for '{Slug}'", slug);
            return null;
        }
    }

    public List<(string Slug, SaveData Data, long SizeBytes)> ListSaves()
    {
        if (!Directory.Exists(SavesDir)) return [];

        List<(string, SaveData, long)> result = [];

        foreach (DirectoryInfo dir in new DirectoryInfo(SavesDir).GetDirectories())
        {
            SaveData? data = GetSaveData(dir.Name);
            if (data == null) continue;
            long size = dir.GetFiles("*.db").Sum(f => f.Length);
            result.Add((dir.Name, data, size));
        }

        return [..result.OrderByDescending(x => x.Item2.LastPlayedAt ?? x.Item2.CreatedAt)];
    }

    public bool SaveCurrent(string name, List<string> modlist)
    {
        try
        {
            string slug = GenerateUniqueSlug(name);
            string saveDir = Path.Combine(SavesDir, slug);
            Directory.CreateDirectory(saveDir);

            foreach (FileInfo file in GetActiveDbFiles())
                File.Move(file.FullName, Path.Combine(saveDir, file.Name), overwrite: true);

            WriteMetadata(saveDir, new SaveData
            {
                Name = name,
                Modlist = modlist,
                CreatedAt = DateTime.UtcNow,
                LastPlayedAt = DateTime.UtcNow
            });

            if (File.Exists(MarkerFilePath)) File.Delete(MarkerFilePath);

            logger.LogDebug("Save '{Name}' created as '{Slug}'", name, slug);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save current!");
            return false;
        }
    }

    public bool DiscardCurrent()
    {
        try
        {
            foreach (FileInfo file in GetActiveDbFiles()) file.Delete();
            if (File.Exists(MarkerFilePath)) File.Delete(MarkerFilePath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discard current save!");
            return false;
        }
    }

    public bool LoadSave(string slug)
    {
        try
        {
            string saveDir = Path.Combine(SavesDir, slug);
            if (!Directory.Exists(saveDir)) return false;

            foreach (FileInfo file in GetActiveDbFiles()) file.Delete();

            foreach (FileInfo file in new DirectoryInfo(saveDir).GetFiles("*.db"))
                File.Copy(file.FullName, Path.Combine(GameSavedDir, file.Name), overwrite: true);

            File.WriteAllText(MarkerFilePath, slug);

            SaveData? data = GetSaveData(slug);
            if (data != null)
            {
                data.LastPlayedAt = DateTime.UtcNow;
                WriteMetadata(saveDir, data);
            }

            logger.LogDebug("Save '{Slug}' loaded.", slug);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load save '{Slug}'!", slug);
            return false;
        }
    }

    public bool CreateNewSave(string name, List<string> modlist)
    {
        try
        {
            string slug = GenerateUniqueSlug(name);
            string saveDir = Path.Combine(SavesDir, slug);
            Directory.CreateDirectory(saveDir);

            WriteMetadata(saveDir, new SaveData
            {
                Name = name,
                Modlist = modlist,
                CreatedAt = DateTime.UtcNow,
            });

            logger.LogDebug("New save '{Name}' created as '{Slug}'", name, slug);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create new save!");
            return false;
        }
    }

    public bool DeleteSave(string slug)
    {
        try
        {
            string saveDir = Path.Combine(SavesDir, slug);
            if (!Directory.Exists(saveDir)) return false;

            Directory.Delete(saveDir, true);

            if (GetCurrentSaveSlug() == slug && File.Exists(MarkerFilePath))
                File.Delete(MarkerFilePath);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete save '{Slug}'!", slug);
            return false;
        }
    }

    public void UpdateLastPlayed(string slug)
    {
        try
        {
            SaveData? data = GetSaveData(slug);
            if (data == null) return;
            data.LastPlayedAt = DateTime.UtcNow;
            WriteMetadata(Path.Combine(SavesDir, slug), data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update last played for '{Slug}'!", slug);
        }
    }

    public bool UpdateSave(string slug, List<string> modlist)
    {
        try
        {
            string saveDir = Path.Combine(SavesDir, slug);
            if (!Directory.Exists(saveDir)) return false;

            foreach (FileInfo file in GetActiveDbFiles())
                File.Move(file.FullName, Path.Combine(saveDir, file.Name), overwrite: true);

            SaveData? data = GetSaveData(slug);
            if (data != null)
            {
                data.Modlist = modlist;
                data.LastPlayedAt = DateTime.UtcNow;
                WriteMetadata(saveDir, data);
            }

            if (File.Exists(MarkerFilePath)) File.Delete(MarkerFilePath);

            logger.LogDebug("Save '{Slug}' updated.", slug);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update save '{Slug}'!", slug);
            return false;
        }
    }

    public bool RenameSave(string slug, string newName)
    {
        try
        {
            SaveData? data = GetSaveData(slug);
            if (data == null) return false;

            data.Name = newName;
            WriteMetadata(Path.Combine(SavesDir, slug), data);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename save '{Slug}'!", slug);
            return false;
        }
    }

    private void WriteMetadata(string saveDir, SaveData data) =>
        File.WriteAllText(Path.Combine(saveDir, SaveMetaFileName), JsonSerializer.Serialize(data, _jsonOptions));

    private static string GenerateUniqueSlug(string name)
    {
        string baseName = new string(name.ToLower().Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(baseName)) baseName = "save";

        string slug = baseName;
        int counter = 2;
        while (Directory.Exists(Path.Combine(SavesDir, slug)))
            slug = $"{baseName}{counter++}";

        return slug;
    }
}