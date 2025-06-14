using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;

namespace Conay.Services;

public class RemotePresets(ModList modList, string name, string indexUrl, string serversDirectory) : IPresetService
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
            Console.WriteLine($"Failed to parse server list from {indexUrl}: {ex.Message}");
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
            Console.WriteLine($"Failed to parse {fileName}: {ex.Message}");
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