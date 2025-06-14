using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Conay.Data;

namespace Conay.Services;

public class LauncherConfig
{
    public readonly Config Data;
    private readonly string _configPath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    private CancellationTokenSource? _saveToken;

    public LauncherConfig()
    {
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
            Console.WriteLine($"Failed to read config: {ex.Message}");
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

    public async Task ScheduleConfigSave()
    {
        if (_saveToken != null)
        {
            await _saveToken.CancelAsync();
        }

        _saveToken = new CancellationTokenSource();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), _saveToken.Token);
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
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}