using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class LocalPresets : IPresetService
{
    private readonly ModList _modList;
    private readonly ILogger<LocalPresets> _logger;
    private Dictionary<string, ServerData>? _presetsCache;
    private readonly string _presetsPath;

    public string GetProviderName() => "local";

    public LocalPresets(ModList modList, ILogger<LocalPresets> logger)
    {
        _modList = modList;
        _logger = logger;

        string appDirectory = AppContext.BaseDirectory;
        _presetsPath = Path.GetFullPath(Path.Combine(appDirectory, "servers"));
    }

    private Dictionary<string, ServerData> GetLocalPresets()
    {
        if (_presetsCache != null)
            return _presetsCache;

        _presetsCache = new Dictionary<string, ServerData>
        {
            ["_vanilla"] = new ServerData
            {
                Name = "Vanilla (no mods)",
                Ip = string.Empty,
                FileName = "_vanilla",
                Mods = []
            }
        };

        if (!Directory.Exists(_presetsPath))
            return _presetsCache;

        foreach (string filePath in Directory.EnumerateFiles(_presetsPath, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                ServerData? preset = JsonSerializer.Deserialize<ServerData>(json);
                if (preset == null) continue;
                preset.FileName = Path.GetFileNameWithoutExtension(filePath);
                _presetsCache[preset.FileName] = preset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load local preset ({Path})!", filePath);
            }
        }

        _logger.LogDebug("Loaded {Count} local presets", _presetsCache.Count);

        return _presetsCache;
    }

    public Task<List<ServerInfo>> GetServerList()
    {
        Dictionary<string, ServerData> presets = GetLocalPresets();
        List<ServerInfo> servers = presets.Values
            .Select(x => new ServerInfo { File = x.FileName ?? string.Empty, Name = x.Name, Provider = this })
            .ToList();

        return Task.FromResult(servers);
    }

    public async Task<ServerData?> FetchServerData(string fileName)
    {
        GetLocalPresets().TryGetValue(fileName, out ServerData? preset);
        if (preset == null) return null;
        preset.Ip = await DnsHelper.ResolveToIpv4Async(preset.Ip, _logger);
        return preset;
    }

    private void ClearCache() => _presetsCache = null;

    public void SavePreset(string fileName, ServerData data)
    {
        if (!Directory.Exists(_presetsPath))
            Directory.CreateDirectory(_presetsPath);

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        try
        {
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(Path.Combine(_presetsPath, fileName + ".json"), json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save preset ({FileName})!", fileName);
            return;
        }

        ClearCache();
    }

    public void DeletePreset(string fileName)
    {
        string filePath = Path.Combine(_presetsPath, fileName + ".json");
        if (File.Exists(filePath))
            File.Delete(filePath);
        ClearCache();
    }

    public void SaveModlistFromPreset(string fileName)
    {
        if (!GetLocalPresets().TryGetValue(fileName, out ServerData? data))
            return;

        GameVersion version = string.IsNullOrEmpty(data.Version)
            ? GameVersionHelper.Current
            : GameVersionHelper.FromPresetVersion(data.Version);
        _modList.SaveModList(data.Mods.ToArray(), version);
    }
}