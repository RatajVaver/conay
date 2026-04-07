using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Conay.Data;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class RemotePresets(
    ILogger<RemotePresets> logger,
    LauncherConfig config,
    HttpService http,
    ModList modList,
    string name,
    string indexUrl,
    string serversDirectory) : IPresetService
{
    private readonly List<ServerData> _presetsCache = [];
    private readonly Lock _cacheLock = new();

    public string GetProviderName() => name;

    public async Task<List<ServerInfo>> GetServerList()
    {
        string json = await http.Get(indexUrl);
        List<ServerInfo> servers;

        if (json == string.Empty)
        {
            logger.LogError("Failed to load server list from: {URL}", indexUrl);
            return LoadFromCache();
        }

        try
        {
            servers = JsonSerializer.Deserialize<List<ServerInfo>>(json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse server list from: {URL}", indexUrl);
            return LoadFromCache();
        }

        foreach (ServerInfo server in servers)
        {
            server.Provider = this;
        }

        SaveToCache(json);

        return servers;
    }

    public async Task<ServerData?> FetchServerData(string fileName)
    {
        lock (_cacheLock)
        {
            ServerData? cached = _presetsCache.Find(x => x.FileName == fileName);
            if (cached != null) return cached;
        }

        string json = await http.Get($"{serversDirectory}/{fileName}.json");

        if (json == string.Empty)
        {
            logger.LogError("Failed to fetch server data for: {File}", fileName);
            return null;
        }

        ServerData? preset;
        try
        {
            preset = JsonSerializer.Deserialize<ServerData>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse: {File}", fileName);
            return null;
        }

        if (preset == null) return null;

        preset.FileName = fileName;
        lock (_cacheLock) _presetsCache.Add(preset);

        return preset;
    }

    public void SaveModlistFromPreset(string fileName)
    {
        ServerData? data = _presetsCache.Find(x => x.FileName == fileName);
        if (data == null)
            return;

        modList.SaveModList(data.Mods.ToArray());
    }

    private List<ServerInfo> LoadFromCache()
    {
        if (!config.Data.UseCache ||
            !File.Exists(Path.Combine(AppContext.BaseDirectory, "cache", $"{name}.json"))) return [];

        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cache", $"{name}.json"));
        List<ServerInfo> servers = JsonSerializer.Deserialize<List<ServerInfo>>(json) ?? [];

        foreach (ServerInfo server in servers)
        {
            server.Provider = this;
        }

        return servers;
    }

    private void SaveToCache(string jsonData)
    {
        if (!config.Data.UseCache) return;

        try
        {
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create cache folder!");
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cache", $"{name}.json"), jsonData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save '{Name}' server list into cache!", name);
        }
    }
}