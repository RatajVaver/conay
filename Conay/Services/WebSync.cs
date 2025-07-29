using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    private List<ExternalModInfo> _mods = [];
    private bool _loading;
    private bool _loaded;

    private async Task FetchModIndex()
    {
        _loading = true;

        string json = await http.Get(indexUrl);
        if (string.IsNullOrEmpty(json))
        {
            logger.LogError("Failed to load external mod index!");
            _loading = false;
            return;
        }

        try
        {
            ExternalSourceData? mods = JsonSerializer.Deserialize<ExternalSourceData>(json);
            if (mods != null)
            {
                _mods = mods.Mods;
                _loaded = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse external mod index!");
        }
        finally
        {
            _loading = false;
        }
    }

    public async Task<ExternalModInfo?> GetModInfo(string fileName)
    {
        if (_loaded)
            return _mods.Find(x => x.FileName == fileName);

        if (!_loading)
        {
            await FetchModIndex();
        }

        while (_loading)
        {
            await Task.Delay(100);
        }

        return _mods.Find(x => x.FileName == fileName);
    }

    public async Task CheckModUpdates(string[] modNames)
    {
        notifyService.UpdateStatus(this, "Checking external mod updates..");

        foreach (string pakName in modNames)
        {
            ExternalModInfo? mod = await GetModInfo(pakName);
            if (mod == null) continue;

            DateTime localLastUpdated = modList.GetLocalModFileLastUpdate("@" + sourceName, pakName);

            if (Epoch.ToDateTime(mod.LastUpdate) < localLastUpdated) continue;

            logger.LogDebug("Needs update: {Mod}", mod.Title ?? pakName);
            _updateQueue.Add(mod);
        }

        if (_updateQueue.Count > 0)
        {
            await UpdateMods();
        }

        notifyService.UpdateStatus(this, "External mods are up to date!");
    }

    private async Task UpdateMods()
    {
        notifyService.UpdateProgress(this, 0);

        while (_updateQueue.Count > 0)
        {
            ExternalModInfo mod = _updateQueue.First();

            notifyService.UpdateStatus(this,
                $"Updating {_updateQueue.Count} {(_updateQueue.Count == 1 ? "mod" : "mods")} ({mod.Title ?? mod.FileName})..");

            bool success = await http.Download($"{modsUrl}/{mod.FileName}.pak",
                $"{modList.LocalModsPath}/@{sourceName}/{mod.FileName}.pak", new Progress<float>(ReportProgress));
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