using System;
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
    {
        _logger = logger;
        _notifyService = notifyService;
        Data = new Config();

        string appDirectory = AppContext.BaseDirectory;
        _configPath = Path.GetFullPath(Path.Combine(appDirectory, "config.json"));

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
        if (IsServerFavorite(fileName)) return;
        Data.Favorites.Add(fileName);
        _ = ScheduleConfigSave();
    }

    public void UnfavoriteServer(string fileName)
    {
        if (!IsServerFavorite(fileName)) return;
        Data.Favorites.Remove(fileName);
        _ = ScheduleConfigSave();
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
        if (!Directory.Exists("cache")) return;

        try
        {
            Directory.Delete("cache", true);
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

    private void SaveConfig()
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