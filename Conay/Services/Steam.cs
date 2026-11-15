using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
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
using Steamworks.Data;
using Steamworks.Ugc;

namespace Conay.Services;

public class Steam : IModSource, ISteamConnection, IDisposable
{
    private const string WorkshopApiUrl =
        "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

    private static readonly TimeSpan SteamStartupGrace = TimeSpan.FromSeconds(20);

    private readonly ILogger<Steam> _logger;
    private readonly HttpService _http;
    private readonly NotifyService _notifyService;

    private bool _isInitialized;
    private bool _isLoggedIn;
    private bool _isConanInstalled;
    private bool _suspended;
    private bool _watchingStartup;
    private string _steamAccountName = string.Empty;

    private readonly List<Item> _updateQueue = [];
    private bool _updating;
    private static readonly SemaphoreSlim Semaphore = new(6, 6);

    private readonly DispatcherTimer _syncTimer;
    private readonly DispatcherTimer _callbackTimer;
    private bool _disposed;

    public string AppInstallDir { get; private set; } = string.Empty;
    private string _workshopPath = string.Empty;

    public bool DualInstallMode { get; private set; }
    private string EnhancedInstallDir { get; set; } = string.Empty;
    private string LegacyInstallDir { get; set; } = string.Empty;
    public string CustomLegacyDir { get; private set; } = string.Empty;

    public void SetCustomLegacyDir(string? path)
    {
        CustomLegacyDir = path ?? string.Empty;
        DetectDualInstall();
    }

    public string GetInstallDirForVersion(GameVersion version) =>
        version == GameVersion.Enhanced
            ? (string.IsNullOrEmpty(EnhancedInstallDir) ? AppInstallDir : EnhancedInstallDir)
            : (string.IsNullOrEmpty(LegacyInstallDir) ? AppInstallDir : LegacyInstallDir);

    public Steam(ILogger<Steam> logger, HttpService http, NotifyService notifyService)
    {
        _logger = logger;
        _http = http;
        _notifyService = notifyService;

        SteamUGC.OnDownloadItemResult += OnModDownloadResult;

        // A switch handed over from the previous instance closes Steam, which waits until Steam has let go of it
        AppRestart.WaitForPreviousInstance(TimeSpan.FromSeconds(10));

        // That switch closes Steam, so stay away from it until SteamAccounts has finished it and resumes
        if (SwitchRequest.Current != null)
        {
            _suspended = true;
        }
        else
        {
            Initialize();

            if (!_isInitialized && !IsSteamRunning())
            {
                LaunchSteam();
            }
        }

        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _syncTimer.Tick += (_, _) => SyncSteam();
        _syncTimer.Start();

        _callbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _callbackTimer.Tick += (_, _) => SteamClient.RunCallbacks();
        _callbackTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _syncTimer.Stop();
        _callbackTimer.Stop();

        if (_isInitialized)
        {
            _isInitialized = false;
            SteamClient.Shutdown();
        }
    }

    private void SyncSteam()
    {
        if (_suspended) return;

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
        // Conay connects once the Steam it started got through starting up, the watcher reports how that goes
        if (_watchingStartup) return;

        switch (GetClientState())
        {
            case ClientState.NotRunning:
                _notifyService.UpdateStatus(this, "Steam is not running! Start it to continue.");
                return;
            case ClientState.Starting:
                _notifyService.UpdateStatus(this, "Waiting for Steam to start..");
                return;
        }

        _notifyService.UpdateStatus(this, "Connecting to Steam..");

        try
        {
            SteamClient.Init(GameVersionHelper.AppId);

            _isInitialized = SteamClient.IsValid;
            _isLoggedIn = SteamClient.IsLoggedOn;

            LoadBaseData();

            _logger.LogDebug("Steam OK: {AccountName}", _steamAccountName);
            _logger.LogDebug("Conan Installed: {IsInstalled}", _isConanInstalled);

            _notifyService.UpdateStatus(this, "Connected!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Steam!");
            _isInitialized = false;
            _isLoggedIn = false;
        }
    }

    public static bool IsSteamRunning()
    {
        Process[] processes = Process.GetProcessesByName("steam");
        foreach (Process process in processes)
            process.Dispose();

        return processes.Length > 0;
    }

    internal enum ClientState
    {
        NotRunning,
        Starting,
        Running
    }

    // Conay only connects to a Steam that's done starting up, trying while it's closed only fails
    internal static ClientState GetClientState(IReadOnlyCollection<DateTime?> startTimes, DateTime now)
    {
        if (startTimes.Count == 0) return ClientState.NotRunning;

        return startTimes.Any(startTime => now - startTime < SteamStartupGrace)
            ? ClientState.Starting
            : ClientState.Running;
    }

    private static ClientState GetClientState() => GetClientState(GetClientStartTimes(), DateTime.Now);

    // When each Steam process started, null if it can't be inspected (e.g. Steam runs as administrator)
    private static List<DateTime?> GetClientStartTimes()
    {
        List<DateTime?> startTimes = [];
        foreach (Process process in Process.GetProcessesByName("steam"))
        {
            try
            {
                startTimes.Add(process.StartTime);
            }
            catch
            {
                startTimes.Add(null);
            }
            finally
            {
                process.Dispose();
            }
        }

        return startTimes;
    }

    public ulong CurrentSteamId
    {
        get
        {
            if (!_isInitialized) return 0;

            try
            {
                return SteamClient.SteamId.Value;
            }
            catch
            {
                return 0;
            }
        }
    }

    public bool IsUpdatingMods => _updating;

    public void Disconnect()
    {
        _suspended = true;
        _isInitialized = false;
        _isLoggedIn = false;

        try
        {
            SteamClient.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect from Steam!");
        }
    }

    public void Resume() => _suspended = false;

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

        try
        {
            Query query = Query.Items.WhereUserSubscribed(SteamClient.SteamId).SortByUpdateDate();
            int pageNumber = 1;

            while (true)
            {
                ResultPage? page = await query.GetPageAsync(pageNumber);
                if (page == null || page.Value.ResultCount == 0) break;

                foreach (Item entry in page.Value.Entries)
                {
                    if (entry.Result != Result.OK) continue;

                    DateTime localLastUpdated = ModList.GetModFileLastUpdate(_workshopPath, entry.Id.Value);

                    if (!entry.NeedsUpdate && entry.Updated < localLastUpdated) continue;

                    _logger.LogDebug("Needs update: {Mod}", entry.Title);
                    QueueModUpdate(entry);
                }

                pageNumber++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Subscribed mods check was interrupted!");
            return;
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

        _notifyService.UpdateStatus(this, "Subscribed mods are up to date!");
    }

    private async Task<List<ulong>?> GetModsInNeedOfUpdate(ulong[] modIds)
    {
        List<KeyValuePair<string, string>> postData = modIds
            .Select((modId, i) => new KeyValuePair<string, string>($"publishedfileids[{i}]", modId.ToString()))
            .ToList();

        postData.Add(new KeyValuePair<string, string>("itemcount", modIds.Length.ToString()));

        FormUrlEncodedContent encoded = new(postData);
        string json = await _http.Post(WorkshopApiUrl, encoded);
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("Failed to check mod updates through API (slower fallback will be used)");
            return null;
        }

        PublishedFileDetails[]? mods = null;

        try
        {
            JsonDocument? response = JsonSerializer.Deserialize<JsonDocument>(json);

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
            if (!ulong.TryParse(mod.Id, out ulong modId))
            {
                _logger.LogWarning("Skipping mod with unparseable ID: {Id}", mod.Id);
                continue;
            }

            DateTime remoteLastUpdated = Epoch.ToDateTime(mod.LastUpdate ?? 0);
            DateTime localLastUpdated = ModList.GetModFileLastUpdate(_workshopPath, modId);
            if (remoteLastUpdated > localLastUpdated)
            {
                needUpdate.Add(modId);
            }
        }

        return needUpdate;
    }

    public async Task<List<string>> CheckModUpdates(ulong[] modIds, GameVersion version = GameVersion.Enhanced)
    {
        _notifyService.UpdateStatus(this, "Checking mod updates..");
        await WaitForSteam();

        string incompatibleTag = version == GameVersion.Enhanced ? "legacy" : "enhanced";
        List<string> incompatible = [];

        int failedCount = 0;

        List<ulong>? mods = await GetModsInNeedOfUpdate(modIds);
        if (mods == null)
        {
            foreach (ulong modId in modIds)
            {
                Item? mod = await Item.GetAsync(modId, 180);
                if (mod == null)
                {
                    failedCount++;
                    continue;
                }

                _notifyService.UpdateStatus(this, $"Checking mod updates ({mod.Value.Title})..");

                if (mod.Value.Tags?.Any(t => t.Equals(incompatibleTag, StringComparison.OrdinalIgnoreCase)) == true)
                    incompatible.Add(mod.Value.Title);

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

                if (mod.Value.Tags?.Any(t => t.Equals(incompatibleTag, StringComparison.OrdinalIgnoreCase)) == true)
                    incompatible.Add(mod.Value.Title);

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

        _notifyService.UpdateStatus(this, failedCount > 0
            ? $"Steam mods are up to date! (could not check {failedCount} mod{(failedCount == 1 ? "" : "s")})"
            : "Steam mods are up to date!");

        return incompatible;
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

            if (mod.Result is Result.AccessDenied or Result.FileNotFound)
            {
                _notifyService.UpdateStatus(this,
                    $"Mod #{mod.Id} cannot be updated (not accessible on Steam Workshop)");
                _logger.LogWarning("Could not update mod #{Mod} (not accessible on Steam Workshop)!", mod.Id);
                await Task.Delay(500);
                _updateQueue.RemoveAt(0);
                continue;
            }

            _notifyService.UpdateStatus(this,
                $"Updating {_updateQueue.Count} {(_updateQueue.Count == 1 ? "mod" : "mods")} ({mod.Title})..");

            bool started = mod.Download();
            if (!started)
            {
                _logger.LogWarning("Could not start mod download ({Mod})!", mod.Title);
            }

            await MonitorDownloadProgress(mod);

            if (_updateQueue.Count > 0)
            {
                _updateQueue.RemoveAt(0);
            }
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
            _logger.LogWarning("Mod download failed: {Result}", result);
        }

        _logger.LogDebug("Download complete");
    }

    public async Task<ModInfo?> GetModData(ulong modId)
    {
        Item? mod;
        try
        {
            mod = await Item.GetAsync(modId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get mod data ({Mod})!", modId);
            return null;
        }

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

        _isConanInstalled = SteamApps.IsSubscribed && SteamApps.IsAppInstalled(GameVersionHelper.AppId);
        _steamAccountName = SteamClient.Name;
        AppInstallDir = SteamApps.AppInstallDir(GameVersionHelper.AppId);
        _workshopPath =
            Path.GetFullPath(Path.Combine(AppInstallDir, $"../../workshop/content/{GameVersionHelper.AppId}"));

        string? betaName = SteamApps.CurrentBetaName;
        GameVersionHelper.Current = GameVersionHelper.FromSteamBranch(betaName);
        _logger.LogDebug("Steam branch: {Branch} -> {Version}", betaName ?? "public", GameVersionHelper.Current);

        DetectDualInstall();
    }

    public bool IsGameDownloading
    {
        get
        {
            if (string.IsNullOrEmpty(AppInstallDir)) return false;
            string acfPath = Path.GetFullPath(
                Path.Combine(AppInstallDir, $"../../appmanifest_{GameVersionHelper.AppId}.acf"));
            if (!File.Exists(acfPath)) return false;
            try
            {
                foreach (string line in File.ReadAllLines(acfPath))
                {
                    string trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("\"StateFlags\"")) continue;
                    string[] parts = trimmed.Split('"');
                    if (parts.Length >= 4 && int.TryParse(parts[3], out int flags))
                        return (flags & (256 | 1024 | 1048576 | 2097152 | 4194304)) != 0;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }

    public bool IsGameRunning
    {
        get
        {
            List<Process> processes = GetGameProcesses();
            foreach (Process process in processes)
                process.Dispose();

            return processes.Count > 0;
        }
    }

    public static List<Process> GetGameProcesses()
    {
        List<Process> game = [];
        foreach (Process process in Process.GetProcesses())
        {
            if (IsGameProcess(process))
                game.Add(process);
            else
                process.Dispose();
        }

        return game;
    }

    private static bool IsGameProcess(Process process)
    {
        try
        {
            string name = process.ProcessName;
            if (OperatingSystem.IsLinux() && !name.StartsWith("ConanSandbox", StringComparison.OrdinalIgnoreCase))
                name = GetWineExecutableName(process.Id);

            return name.StartsWith("ConanSandbox", StringComparison.OrdinalIgnoreCase) &&
                   !name.Contains("Server", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string GetWineExecutableName(int pid)
    {
        string exe = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0')[0];
        return Path.GetFileNameWithoutExtension(exe.Replace('\\', '/'));
    }

    private void DetectDualInstall()
    {
        if (string.IsNullOrEmpty(AppInstallDir)) return;

        string common = Path.GetFullPath(Path.Combine(AppInstallDir, ".."));
        const string exeRel = "ConanSandbox/Binaries/Win64/ConanSandbox.exe";

        string eDir = Path.Combine(common, "Conan Exiles Enhanced");
        string lDir = Path.Combine(common, "Conan Exiles Legacy");

        EnhancedInstallDir = File.Exists(Path.Combine(eDir, exeRel)) ? eDir : string.Empty;

        if (!string.IsNullOrEmpty(CustomLegacyDir) && Directory.Exists(CustomLegacyDir))
            LegacyInstallDir = CustomLegacyDir;
        else
            LegacyInstallDir = File.Exists(Path.Combine(lDir, exeRel)) ? lDir : string.Empty;

        if (GameVersionHelper.Current == GameVersion.Enhanced && string.IsNullOrEmpty(EnhancedInstallDir))
            EnhancedInstallDir = AppInstallDir;
        else if (GameVersionHelper.Current == GameVersion.Legacy && string.IsNullOrEmpty(LegacyInstallDir))
            LegacyInstallDir = AppInstallDir;

        DualInstallMode = !string.IsNullOrEmpty(EnhancedInstallDir) && !string.IsNullOrEmpty(LegacyInstallDir);
        if (DualInstallMode)
            _logger.LogDebug("Dual install mode active. Enhanced: {E}, Legacy: {L}", EnhancedInstallDir,
                LegacyInstallDir);
    }

    public void LaunchSteam()
    {
        _notifyService.UpdateStatus(this, "Launching Steam..");
        StartClient(_logger);

        // Steam can crash while it starts up on some PCs (see SteamStartupWatcher), so make sure it gets going
        if (OperatingSystem.IsWindows() && !_watchingStartup)
            _ = WatchStartup();
    }

    [SupportedOSPlatform("windows")]
    private async Task WatchStartup()
    {
        _watchingStartup = true;

        try
        {
            using SteamClientProcesses processes = new(_logger);
            await new SteamStartupWatcher(_logger, _notifyService, processes).Watch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to watch Steam start!");
        }
        finally
        {
            _watchingStartup = false;
        }
    }

    // Starts Steam like it's started by hand (with its window), without the game settings the Steam API put into
    // this process
    internal static void StartClient(ILogger logger)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                if (DetachedProcess.Start("steam", ["steam"], startupEnvironment: true))
                    return;
            }
            else if (FindClientExecutable() is { } steamExe)
            {
                using Process? process = Process.Start(CreateClientStartInfo(steamExe, StartupEnvironment.Variables));
                if (process != null)
                    return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch Steam!");
        }

        Protocol.Open("steam://open/main");
    }

    private static string? FindClientExecutable()
    {
        if (SteamPaths.FindSteamRoot() is not { } root) return null;

        string steamExe = Path.GetFullPath(Path.Combine(root, "steam.exe"));
        return File.Exists(steamExe) ? steamExe : null;
    }

    internal static ProcessStartInfo CreateClientStartInfo(string steamExe,
        IReadOnlyDictionary<string, string> startupEnvironment)
    {
        ProcessStartInfo startInfo = new(steamExe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(steamExe)
        };

        StartupEnvironment.Apply(startInfo.Environment, startupEnvironment);
        return startInfo;
    }

    public static void OpenWorkshopPage(ulong modId)
    {
        Protocol.Open("steam://url/CommunityFilePage/" + modId);
    }

    public async Task SubscribeToMods(ulong[] modIds)
    {
        _notifyService.UpdateStatus(this, "Subscribing to mods..");
        await WaitForSteam();

        PublishedFileId[] mods = modIds.Select(modId => (PublishedFileId)modId).ToArray();
        Query query = Query.Items.WithFileId(mods);
        ResultPage? page = await query.GetPageAsync(1);
        if (page is { ResultCount: > 0 })
        {
            foreach (Item entry in page.Value.Entries)
            {
                if (entry.IsSubscribed) continue;
                _notifyService.UpdateStatus(this, $"Subscribing to mods ({entry.Title})..");
                await entry.Subscribe();
            }
        }
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
                    Ping = Environment.TickCount - startTime
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