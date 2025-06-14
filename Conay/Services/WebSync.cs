using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;

namespace Conay.Services;

public class WebSync : IModSource
{
    private readonly ModList _modList;
    private readonly string _sourceName;
    private readonly string _indexUrl;
    private readonly string _modsUrl;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    private readonly List<ExternalModInfo> _updateQueue = [];
    private List<ExternalModInfo> _mods = [];
    private bool _loaded;

    public WebSync(ModList modList, string sourceName, string indexUrl, string modsUrl)
    {
        _modList = modList;
        _sourceName = sourceName;
        _indexUrl = indexUrl;
        _modsUrl = modsUrl;
        _ = FetchModIndex();
    }

    private async Task FetchModIndex()
    {
        string json = await Web.Get(_indexUrl);

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
            Console.WriteLine($"Failed to parse external mod index: {ex.Message}");
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
        StatusChanged?.Invoke(this, "Checking external mod updates..");

        foreach (string pakName in modNames)
        {
            ExternalModInfo? mod = await GetModInfo(pakName);
            if (mod == null) continue;

            DateTime localLastUpdated = _modList.GetLocalModFileLastUpdate("@" + _sourceName, pakName);

            if (Epoch.ToDateTime(mod.LastUpdate) < localLastUpdated) continue;

            Console.WriteLine($"Needs update: {mod.Title ?? pakName}");
            _updateQueue.Add(mod);
        }

        if (_updateQueue.Count > 0)
        {
            await UpdateMods();
        }

        StatusChanged?.Invoke(this, "External mods are up to date!");
    }

    private async Task UpdateMods()
    {
        DownloadProgressChanged?.Invoke(this, 0);

        while (_updateQueue.Count > 0)
        {
            ExternalModInfo mod = _updateQueue.First();

            StatusChanged?.Invoke(this,
                $"Updating {_updateQueue.Count} {(_updateQueue.Count == 1 ? "mod" : "mods")} ({mod.Title ?? mod.FileName})..");

            await Web.Download($"{_modsUrl}/{mod.FileName}.pak",
                $"{_modList.LocalModsPath}/@{_sourceName}/{mod.FileName}.pak", new Progress<float>(ReportProgress));

            _updateQueue.RemoveAt(0);
        }

        DownloadProgressChanged?.Invoke(this, 100);
    }

    private void ReportProgress(float progress)
    {
        DownloadProgressChanged?.Invoke(this, progress * 100);
    }
}