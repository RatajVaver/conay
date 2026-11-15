using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Conay.Data;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class LauncherConfig
{
    private readonly ILogger<LauncherConfig> _logger;
    private readonly NotifyService _notifyService;
    public readonly Config Data;
    private readonly string _configPath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    private CancellationTokenSource? _saveToken;

    public LauncherConfig(ILogger<LauncherConfig> logger, NotifyService notifyService)
        : this(logger, notifyService, Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "config.json")))
    {
    }

    internal LauncherConfig(ILogger<LauncherConfig> logger, NotifyService notifyService, string configPath)
    {
        _logger = logger;
        _notifyService = notifyService;
        Data = new Config();
        _configPath = configPath;

        if (!File.Exists(_configPath)) return;

        try
        {
            string json = File.ReadAllText(_configPath);
            Config? config = JsonSerializer.Deserialize<Config>(json);
            if (config != null)
                Data = config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read config!");
        }
    }

    public bool IsServerFavorite(string fileName)
    {
        return Data.Favorites.Contains(fileName);
    }

    public void FavoriteServer(string fileName)
    {
        if (!Data.Favorites.Add(fileName)) return;
        _ = ScheduleConfigSave();
    }

    public void UnfavoriteServer(string fileName)
    {
        if (!Data.Favorites.Remove(fileName)) return;
        _ = ScheduleConfigSave();
    }

    public ulong GetServerAccount(string fileName) => Data.ServerAccounts.GetValueOrDefault(fileName);

    public bool AsksForServerAccount(string fileName) => Data.AskAccountServers.Contains(fileName);

    public void SetServerAccount(string fileName, ulong steamId, bool askEveryTime = false)
    {
        bool changed = askEveryTime
            ? Data.AskAccountServers.Add(fileName)
            : Data.AskAccountServers.Remove(fileName);

        if (steamId == 0)
        {
            changed |= Data.ServerAccounts.Remove(fileName);
        }
        else if (GetServerAccount(fileName) != steamId)
        {
            Data.ServerAccounts[fileName] = steamId;
            changed = true;
        }

        if (changed)
            _ = ScheduleConfigSave();
    }

    public void SaveLastLaunchedVersion(GameVersion version)
    {
        Data.LastLaunchedVersion = version;
        SaveConfig();
    }

    public void SaveIntoHistory(string fileName)
    {
        if (!Data.KeepHistory) return;
        Data.History.Remove(fileName);
        Data.History.Insert(0, fileName);
        SaveConfig();
    }

    public void ClearCache()
    {
        string cacheDirectory = Path.Combine(AppContext.BaseDirectory, "cache");
        if (!Directory.Exists(cacheDirectory)) return;

        try
        {
            Directory.Delete(cacheDirectory, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache!");
        }
    }

    public async Task ScheduleConfigSave()
    {
        if (_saveToken != null)
        {
            await _saveToken.CancelAsync();
        }

        _saveToken = new CancellationTokenSource();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), _saveToken.Token);
            SaveConfig();
        }
        catch (TaskCanceledException)
        {
        }
    }

    public void SaveConfig()
    {
        try
        {
            string json = JsonSerializer.Serialize(Data, _options);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            _notifyService.UpdateStatus(this,
                "Failed to save config! Try to run Conay as administrator, or move it into another location.");
            _logger.LogError(ex, "Failed to save config!");
        }
    }
}