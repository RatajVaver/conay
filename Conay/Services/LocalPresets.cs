using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Conay.Data;

namespace Conay.Services;

public class LocalPresets : IPresetService
{
    private readonly ModList _modList;
    private List<ServerData>? _presetsCache;
    private readonly string _presetsPath;

    public string GetProviderName() => "local";

    public LocalPresets(ModList modList)
    {
        _modList = modList;

        string appDirectory = AppContext.BaseDirectory;
        _presetsPath = Path.GetFullPath(Path.Combine(appDirectory, "servers"));
    }

    private List<ServerData> GetLocalPresets()
    {
        if (_presetsCache != null)
            return _presetsCache;

        _presetsCache = [];

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
                _presetsCache.Add(preset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load {filePath}: {ex.Message}");
            }
        }

        Console.WriteLine($"Loaded {_presetsCache.Count} local presets");

        return _presetsCache;
    }

    public Task<List<ServerInfo>> GetServerList()
    {
        List<ServerData> presets = GetLocalPresets();
        List<ServerInfo> servers = [];
        servers.AddRange(presets.Select(x => new ServerInfo { File = x.FileName ?? string.Empty, Name = x.Name, Provider = this }));

        return Task.FromResult(servers);
    }

    public Task<ServerData?> FetchServerData(string fileName)
    {
        List<ServerData> presets = GetLocalPresets();
        return Task.FromResult(presets.Find(x => x.FileName == fileName));
    }

    public void SaveModlistFromPreset(string fileName)
    {
        List<ServerData> presets = GetLocalPresets();
        ServerData? data = presets.Find(x => x.FileName == fileName);
        if (data == null)
            return;

        _modList.SaveModList(data.Mods.ToArray());
    }
}