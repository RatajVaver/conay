using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class RemotePresets(
    ILogger<RemotePresets> logger,
    ModList modList,
    string name,
    string indexUrl,
    string serversDirectory) : IPresetService
{
    private readonly ModList _modList = modList;
    private readonly List<ServerData> _presetsCache = [];

    public string GetProviderName() => name;

    public async Task<List<ServerInfo>> GetServerList()
    {
        string json = await Web.Get(indexUrl);
        List<ServerInfo> servers = [];

        try
        {
            servers = JsonSerializer.Deserialize<List<ServerInfo>>(json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse server list from {URL}!", indexUrl);
        }

        foreach (ServerInfo server in servers)
        {
            server.Provider = this;
        }

        return servers;
    }

    public async Task<ServerData?> FetchServerData(string fileName)
    {
        ServerData? preset = _presetsCache.Find(x => x.FileName == fileName);
        if (preset != null)
            return preset;

        string json = await Web.Get($"{serversDirectory}/{fileName}.json");

        try
        {
            preset = JsonSerializer.Deserialize<ServerData>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse {File}!", fileName);
        }

        if (preset == null) return null;

        preset.FileName = fileName;
        _presetsCache.Add(preset);

        return preset;
    }

    public void SaveModlistFromPreset(string fileName)
    {
        ServerData? data = _presetsCache.Find(x => x.FileName == fileName);
        if (data == null)
            return;

        _modList.SaveModList(data.Mods.ToArray());
    }
}