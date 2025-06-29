﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class WebSync : IModSource
{
    private readonly ILogger<WebSync> _logger;
    private readonly HttpService _http;
    private readonly ModList _modList;
    private readonly NotifyService _notifyService;
    private readonly string _sourceName;
    private readonly string _indexUrl;
    private readonly string _modsUrl;

    private readonly List<ExternalModInfo> _updateQueue = [];
    private List<ExternalModInfo> _mods = [];
    private bool _loaded;

    public WebSync(ILogger<WebSync> logger, HttpService http, ModList modList, NotifyService notifyService,
        string sourceName, string indexUrl, string modsUrl)
    {
        _logger = logger;
        _http = http;
        _modList = modList;
        _notifyService = notifyService;
        _sourceName = sourceName;
        _indexUrl = indexUrl;
        _modsUrl = modsUrl;
        _ = FetchModIndex();
    }

    private async Task FetchModIndex()
    {
        string json = await _http.Get(_indexUrl);

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
            _logger.LogError(ex, "Failed to parse external mod index!");
        }
    }

    public async Task<ExternalModInfo?> GetModInfo(string fileName)
    {
        if (!_loaded)
            await Task.Delay(200);

        return _mods.Find(x => x.FileName == fileName);
    }

    public async Task CheckModUpdates(string[] modNames)
    {
        _notifyService.UpdateStatus(this, "Checking external mod updates..");

        foreach (string pakName in modNames)
        {
            ExternalModInfo? mod = await GetModInfo(pakName);
            if (mod == null) continue;

            DateTime localLastUpdated = _modList.GetLocalModFileLastUpdate("@" + _sourceName, pakName);

            if (Epoch.ToDateTime(mod.LastUpdate) < localLastUpdated) continue;

            _logger.LogDebug("Needs update: {Mod}", mod.Title ?? pakName);
            _updateQueue.Add(mod);
        }

        if (_updateQueue.Count > 0)
        {
            await UpdateMods();
        }

        _notifyService.UpdateStatus(this, "External mods are up to date!");
    }

    private async Task UpdateMods()
    {
        _notifyService.UpdateProgress(this, 0);

        while (_updateQueue.Count > 0)
        {
            ExternalModInfo mod = _updateQueue.First();

            _notifyService.UpdateStatus(this,
                $"Updating {_updateQueue.Count} {(_updateQueue.Count == 1 ? "mod" : "mods")} ({mod.Title ?? mod.FileName})..");

            bool success = await _http.Download($"{_modsUrl}/{mod.FileName}.pak",
                $"{_modList.LocalModsPath}/@{_sourceName}/{mod.FileName}.pak", new Progress<float>(ReportProgress));
            if (!success)
                _logger.LogError("Failed to update mod ({Mod})!", mod.Title ?? mod.FileName);

            _updateQueue.RemoveAt(0);
        }

        _notifyService.UpdateProgress(this, 100);
    }

    private void ReportProgress(float progress)
    {
        _notifyService.UpdateProgress(this, progress * 100);
    }
}