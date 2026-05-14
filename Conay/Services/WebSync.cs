using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class WebSync(
    ILogger<WebSync> logger,
    HttpService http,
    ModList modList,
    NotifyService notifyService,
    string sourceName,
    string indexUrl,
    string modsUrl)
    : IModSource
{
    private readonly List<ExternalModInfo> _updateQueue = [];
    private Dictionary<string, ExternalModInfo> _mods = [];
    private readonly TaskCompletionSource _indexLoaded = new();
    private int _fetchStarted;

    private async Task FetchModIndex()
    {
        try
        {
            string json = await http.Get(indexUrl);
            if (string.IsNullOrEmpty(json))
            {
                logger.LogError("Failed to load external mod index!");
                return;
            }

            ExternalSourceData? data = JsonSerializer.Deserialize<ExternalSourceData>(json);
            if (data != null)
                _mods = data.Mods.ToDictionary(m => m.FileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse external mod index!");
        }
        finally
        {
            _indexLoaded.TrySetResult();
        }
    }

    public async Task<ExternalModInfo?> GetModInfo(string fileName)
    {
        if (!_indexLoaded.Task.IsCompleted)
        {
            if (Interlocked.Exchange(ref _fetchStarted, 1) == 0)
                _ = FetchModIndex();

            await _indexLoaded.Task;
        }

        _mods.TryGetValue(fileName, out ExternalModInfo? mod);
        return mod;
    }

    public async Task CheckModUpdates(string[] modNames, GameVersion version)
    {
        notifyService.UpdateStatus(this, "Checking external mod updates..");

        foreach (string pakName in modNames)
        {
            ExternalModInfo? mod = await GetModInfo(pakName);
            if (mod == null) continue;

            DateTime localLastUpdated = modList.GetLocalModFileLastUpdate("@" + sourceName, pakName, version);

            if (Epoch.ToDateTime(mod.LastUpdate) < localLastUpdated) continue;
            if (_updateQueue.Any(x => x.FileName == mod.FileName)) continue;

            logger.LogDebug("Needs update: {Mod}", mod.Title ?? pakName);
            _updateQueue.Add(mod);
        }

        if (_updateQueue.Count > 0)
        {
            await UpdateMods(version);
        }

        notifyService.UpdateStatus(this, "External mods are up to date!");
    }

    private async Task UpdateMods(GameVersion version)
    {
        notifyService.UpdateProgress(this, 0);

        while (_updateQueue.Count > 0)
        {
            ExternalModInfo mod = _updateQueue.First();

            notifyService.UpdateStatus(this,
                $"Updating {_updateQueue.Count} {(_updateQueue.Count == 1 ? "mod" : "mods")} ({mod.Title ?? mod.FileName})..");

            bool success = await http.Download($"{modsUrl}/{mod.FileName}.pak",
                $"{modList.GetLocalModsPath(version)}/@{sourceName}/{mod.FileName}.pak", new Progress<float>(ReportProgress));
            if (!success)
                logger.LogError("Failed to update mod ({Mod})!", mod.Title ?? mod.FileName);

            _updateQueue.RemoveAt(0);
        }

        notifyService.UpdateProgress(this, 100);
    }

    private void ReportProgress(float progress)
    {
        notifyService.UpdateProgress(this, progress * 100);
    }
}