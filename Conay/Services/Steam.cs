using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;
using SteamQuery;
using SteamQuery.Enums;
using Steamworks;
using Steamworks.Ugc;

namespace Conay.Services;

public class Steam : IModSource
{
    private const uint AppId = 440900;

    private readonly ILogger<Steam> _logger;
    private readonly HttpService _http;
    private readonly NotifyService _notifyService;

    private bool _isInitialized;
    private bool _isLoggedIn;
    private bool _isConanInstalled;
    private string _steamAccountName = string.Empty;

    private readonly List<Item> _updateQueue = [];
    private bool _updating;
    private static readonly SemaphoreSlim Semaphore = new(6, 6);

    public string AppInstallDir { get; private set; } = string.Empty;
    private string _workshopPath = string.Empty;

    public Steam(ILogger<Steam> logger, HttpService http, NotifyService notifyService)
    {
        _logger = logger;
        _http = http;
        _notifyService = notifyService;

        SteamUGC.OnDownloadItemResult += OnModDownloadResult;

        Initialize();

        if (!_isInitialized)
        {
            LaunchSteam();
        }

        DispatcherTimer syncTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        syncTimer.Tick += (_, _) => SyncSteam();
        syncTimer.Start();

        DispatcherTimer callbackTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        callbackTimer.Tick += (_, _) => SteamClient.RunCallbacks();
        callbackTimer.Start();
    }

    ~Steam()
    {
        SteamClient.Shutdown();
    }

    private void SyncSteam()
    {
        try
        {
            _isInitialized = SteamClient.IsValid;
            _isLoggedIn = SteamClient.IsLoggedOn;
        }
        catch
        {
            _isInitialized = false;
            _isLoggedIn = false;
        }

        if (!_isInitialized)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        _notifyService.UpdateStatus(this, "Connecting to Steam..");

        try
        {
            SteamClient.Init(AppId);

            _isInitialized = SteamClient.IsValid;
            _isLoggedIn = SteamClient.IsLoggedOn;

            LoadBaseData();

            _logger.LogDebug("Steam OK: {AccountName}", _steamAccountName);
            _logger.LogDebug("Conan Installed: {IsInstalled}", _isConanInstalled);

            _notifyService.UpdateStatus(this, "Connected!");
        }
        catch
        {
            _isInitialized = false;
            _isLoggedIn = false;
        }
    }

    public async Task WaitForSteam()
    {
        while (!_isInitialized)
        {
            await Task.Delay(200);
        }
    }

    public async Task CheckSubscribedModUpdates()
    {
        _notifyService.UpdateStatus(this, "Checking mod updates..");

        while (!_isLoggedIn)
        {
            _notifyService.UpdateStatus(this, "Waiting for Steam..");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Query query = Query.Items.WhereUserSubscribed(SteamClient.SteamId).SortByUpdateDate();
        int pageNumber = 1;

        while (true)
        {
            ResultPage? page = await query.GetPageAsync(pageNumber);
            if (page == null || page.Value.ResultCount == 0) break;

            foreach (Item entry in page.Value.Entries)
            {
                DateTime localLastUpdated = ModList.GetModFileLastUpdate(_workshopPath, entry.Id.Value);

                if (!entry.NeedsUpdate && entry.Updated < localLastUpdated) continue;

                _logger.LogDebug("Needs update: {Mod}", entry.Title);
                QueueModUpdate(entry);
            }

            pageNumber++;
        }

        if (_updateQueue.Count > 0)
        {
            await UpdateMods();
        }

        _notifyService.UpdateStatus(this, "Subscribed mods are up to date!");
    }

    private async Task<List<ulong>?> GetModsInNeedOfUpdate(ulong[] modIds)
    {
        List<KeyValuePair<string, string>> postData = modIds
            .Select((modId, i) => new KeyValuePair<string, string>($"publishedfileids[{i}]", modId.ToString()))
            .ToList();

        postData.Add(new KeyValuePair<string, string>("itemcount", modIds.Length.ToString()));

        FormUrlEncodedContent encoded = new(postData);
        string json =
            await _http.Post("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                encoded);

        JsonDocument? response = JsonSerializer.Deserialize<JsonDocument>(json);
        PublishedFileDetails[]? mods = null;

        try
        {
            mods = response?.RootElement.GetProperty("response")
                .GetProperty("publishedfiledetails")
                .Deserialize<PublishedFileDetails[]>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse mods to update using API (slower fallback will be used)");
        }

        if (mods == null)
            return null;

        List<ulong> needUpdate = [];

        foreach (PublishedFileDetails mod in mods)
        {
            ulong modId = ulong.Parse(mod.Id);
            DateTime remoteLastUpdated = Epoch.ToDateTime(mod.LastUpdate ?? 0);
            DateTime localLastUpdated = ModList.GetModFileLastUpdate(_workshopPath, modId);
            if (remoteLastUpdated > localLastUpdated)
            {
                needUpdate.Add(modId);
            }
        }

        return needUpdate;
    }

    public async Task CheckModUpdates(ulong[] modIds)
    {
        _notifyService.UpdateStatus(this, "Checking mod updates..");
        await WaitForSteam();

        List<ulong>? mods = await GetModsInNeedOfUpdate(modIds);
        if (mods == null)
        {
            foreach (ulong modId in modIds)
            {
                Item? mod = await Item.GetAsync(modId, 180);
                if (mod == null) continue;

                _notifyService.UpdateStatus(this, $"Checking mod updates ({mod.Value.Title})..");

                DateTime localLastUpdated = ModList.GetModFileLastUpdate(_workshopPath, mod.Value.Id.Value);

                if (!mod.Value.NeedsUpdate && mod.Value.Updated < localLastUpdated) continue;

                _logger.LogDebug("Needs update: {Mod}", mod.Value.Title);
                QueueModUpdate(mod.Value);
            }
        }
        else
        {
            foreach (ulong modId in mods)
            {
                Item? mod = await Item.GetAsync(modId, 180);
                if (mod == null) continue;

                _logger.LogDebug("Needs update: {Mod}", mod.Value.Title);
                QueueModUpdate(mod.Value);
            }
        }

        if (_updating)
        {
            _notifyService.UpdateStatus(this, "Updating mods..");

            while (_updating)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        else if (_updateQueue.Count > 0)
        {
            await UpdateMods();
        }

        _notifyService.UpdateStatus(this, "Steam mods are up to date!");
    }

    private void QueueModUpdate(Item mod)
    {
        if (_updateQueue.Any(x => x.Id == mod.Id)) return;
        _updateQueue.Add(mod);
    }

    private async Task UpdateMods()
    {
        _notifyService.UpdateProgress(this, 0);
        _updating = true;

        while (_updateQueue.Count > 0)
        {
            Item mod = _updateQueue.First();

            _notifyService.UpdateStatus(this,
                $"Updating {_updateQueue.Count} {(_updateQueue.Count == 1 ? "mod" : "mods")} ({mod.Title})..");

            bool started = mod.Download();
            if (!started)
            {
                _logger.LogWarning("Could not start mod download ({Mod})!", mod.Title);
            }

            await MonitorDownloadProgress(mod);
            _updateQueue.RemoveAt(0);
        }

        _updating = false;
        _notifyService.UpdateProgress(this, 100);
    }

    private async Task MonitorDownloadProgress(Item mod)
    {
        while (mod.IsDownloadPending || mod.IsDownloading)
        {
            double progress = mod.DownloadAmount;
            _notifyService.UpdateProgress(this, progress * 100f);
            await Task.Delay(100);
        }
    }

    private void OnModDownloadResult(Result result)
    {
        if (result != Result.OK)
        {
            _logger.LogDebug("Result: {Result}", result);
        }

        _logger.LogDebug("Download complete");
    }

    public async Task<ModInfo?> GetModData(ulong modId)
    {
        Item? mod = await Item.GetAsync(modId);
        if (mod == null) return null;

        ModInfo data = new()
        {
            Id = modId,
            Title = mod.Value.Title,
            Author = mod.Value.Owner.Name,
            WorkshopUrl = mod.Value.Url,
            Size = (int)Math.Ceiling(mod.Value.SizeBytes / 1000000d),
            LastUpdate = new[] { mod.Value.Created, mod.Value.Updated }.Max(),
            Icon = mod.Value.PreviewImageUrl
        };

        return data;
    }

    private void LoadBaseData()
    {
        if (!_isInitialized)
            return;

        _isConanInstalled = SteamApps.IsSubscribed && SteamApps.IsAppInstalled(AppId);
        _steamAccountName = SteamClient.Name;
        AppInstallDir = SteamApps.AppInstallDir(AppId);
        _workshopPath = Path.GetFullPath(Path.Combine(AppInstallDir, "../../workshop/content/440900"));
    }

    private void LaunchSteam()
    {
        _notifyService.UpdateStatus(this, "Launching Steam..");
        Protocol.Open("steam://-/\" -silent");
    }

    public static void OpenWorkshopPage(ulong modId)
    {
        Protocol.Open("steam://url/CommunityFilePage/" + modId);
    }

    public static async Task<ServerQueryResult> QueryServer(string ipAddress, int queryPort, bool retrying = false)
    {
        if (string.IsNullOrEmpty(ipAddress) || queryPort <= 0 || queryPort > 65535)
            return new ServerQueryResult();

        await Semaphore.WaitAsync();

        int startTime = Environment.TickCount;

        return await Task.Run(async () =>
        {
            try
            {
                GameServer server = new(ipAddress, queryPort);
                server.SendTimeout = TimeSpan.FromSeconds(retrying ? 5 : 0.5);
                server.ReceiveTimeout = TimeSpan.FromSeconds(retrying ? 5 : 0.5);

                await server.PerformQueryAsync(SteamQueryA2SQuery.Information);

                ServerQueryResult result = new()
                {
                    ServerName = server.Information.ServerName,
                    Map = server.Information.Map,
                    Players = server.Information.OnlinePlayers,
                    MaxPlayers = server.Information.MaxPlayers,
                    Ping = Environment.TickCount - startTime,
                };

                server.Dispose();
                return result;
            }
            catch
            {
                return new ServerQueryResult();
            }
            finally
            {
                Semaphore.Release();
            }
        });
    }
}