using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Data;
using Conay.Services;
using Conay.Utils;

namespace Conay.ViewModels.Parts;

public partial class ServerPresetViewModel : ViewModelBase, ILazyLoad
{
    private readonly Router _router;
    private readonly Steam _steam;
    private readonly LauncherConfig _launcherConfig;
    private readonly IPresetService _provider;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _ipAddress = "Loading..";

    [ObservableProperty]
    private string _players = string.Empty;

    [ObservableProperty]
    private string _map = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIcon))]
    [NotifyPropertyChangedFor(nameof(ShowDefaultIcon))]
    private string? _icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDiscord))]
    private string? _discord;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWebsite))]
    private string? _website;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoleplay))]
    [NotifyPropertyChangedFor(nameof(IsMechPvP))]
    [NotifyPropertyChangedFor(nameof(IsDicePvP))]
    [NotifyPropertyChangedFor(nameof(HasConaySync))]
    private string[]? _tags;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModded))]
    [NotifyPropertyChangedFor(nameof(ModdedTooltip))]
    private int _modsCount;

    [ObservableProperty]
    private string _playerCountColor = "#fff";

    [ObservableProperty]
    private string _pingColor = "#fff";

    [ObservableProperty]
    private string _ping = "Ping: N/A";

    [ObservableProperty]
    private bool _refreshInProgress;

    public bool ShowIcon => !string.IsNullOrEmpty(Icon)
                            && _launcherConfig.Data is { DisplayIcons: true, OfflineMode: false };

    public bool ShowDefaultIcon => string.IsNullOrEmpty(Icon) && _launcherConfig.Data.DisplayIcons;

    public bool ShowDiscord => !string.IsNullOrEmpty(Discord);
    public bool ShowWebsite => !string.IsNullOrEmpty(Website);

    public bool IsModded => ModsCount > 0;
    public bool IsRoleplay => Tags?.Contains("roleplay") ?? false;
    public bool IsMechPvP => Tags?.Contains("mech") ?? false;
    public bool IsDicePvP => Tags?.Contains("dice") ?? false;
    public bool HasConaySync => Tags?.Contains("sync") ?? false;
    public string ModdedTooltip => $"Modded ({ModsCount} mods)";

    public bool IsLoaded { get; set; }
    public bool IsVisible { get; set; }

    public readonly string File;
    private int? _queryPort;
    private int _failedQueries;

    [ObservableProperty]
    private int _mods;

    private readonly ServerInfo _serverInfo;
    private ServerData? _preset;

    public ServerPresetViewModel(Router router, Steam steam, LauncherConfig launcherConfig, ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
        _router = router;
        _steam = steam;
        _launcherConfig = launcherConfig;
        _provider = serverInfo.Provider!;

        Name = serverInfo.Name;
        Icon = serverInfo.Icon;
        File = serverInfo.File;
        IsFavorite = launcherConfig.IsServerFavorite(File);

        LoadMapAndPlayersFromServerInfo();
    }

    private void LoadMapAndPlayersFromServerInfo()
    {
        if (_launcherConfig.Data is not { QueryServers: true, OfflineMode: false })
            return;

        if (_serverInfo.Map != null)
        {
            Map = _serverInfo.Map;
        }

        if (_serverInfo.Players != null)
        {
            Players = _serverInfo.MaxPlayers != null
                ? $"{_serverInfo.Players} / {_serverInfo.MaxPlayers}"
                : $"~{_serverInfo.Players}  ";

            UpdatePlayerCountColor(_serverInfo.Players, _serverInfo.MaxPlayers);
        }
    }

    private void UpdatePlayerCountColor(int? players, int? maxPlayers)
    {
        if (players == null || maxPlayers == null)
        {
            PlayerCountColor = "#888";
            return;
        }

        if (players >= maxPlayers - 1)
        {
            PlayerCountColor = "#db8a76";
            return;
        }

        PlayerCountColor = "#fff";
    }

    private void UpdatePing(int ping)
    {
        Ping = $"Ping: {ping} ms";
        PingColor = new HslColor(1, Math.Max(0, 130 - ping / 2), 0.58, 0.66).ToRgb().ToString();
    }

    public async Task LoadDataAsync()
    {
        IsLoaded = true;
        await UpdateServerData();
    }

    private async Task UpdateServerData()
    {
        ServerData? preset = await _provider.FetchServerData(_serverInfo.File);
        if (preset != null)
        {
            IpAddress = preset.Ip;
            Discord = preset.Discord;
            Website = preset.Website;
            Tags = preset.Tags;
            ModsCount = preset.Mods.Count;
            _queryPort = preset.QueryPort;
            _preset = preset;
        }

        if (_launcherConfig.Data is { QueryServers: true, OfflineMode: false })
        {
            _ = GetServerOnlineStatus();
        }
    }

    public async Task GetServerOnlineStatus()
    {
        if (_queryPort == null) return;

        RefreshInProgress = true;

        ServerQueryResult result =
            await Steam.QueryServer(IpAddress.Split(':')[0], (int)_queryPort, _failedQueries > 0);
        if (result.MaxPlayers <= 0)
        {
            RefreshInProgress = false;
            _failedQueries++;
            if (_failedQueries <= 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                _ = GetServerOnlineStatus();
            }

            return;
        }

        Players = $"{result.Players} / {result.MaxPlayers}";
        Map = result.Map;

        UpdatePing(result.Ping);
        UpdatePlayerCountColor(result.Players, result.MaxPlayers);
        RefreshInProgress = false;
    }

    [RelayCommand]
    private async Task LaunchServerPreset()
    {
        _router.BeforeLaunch(Name);
        await _steam.WaitForSteam();
        _provider.SaveModlistFromPreset(File);
        _router.ReadyForLaunch(_preset);
    }

    [RelayCommand]
    private void FavoriteServer()
    {
        IsFavorite = true;
        _launcherConfig.FavoriteServer(File);
    }

    [RelayCommand]
    private void UnfavoriteServer()
    {
        IsFavorite = false;
        _launcherConfig.UnfavoriteServer(File);
    }

    [RelayCommand]
    private void OpenDiscordLink()
    {
        if (string.IsNullOrEmpty(Discord)) return;

        string discord = Discord.Replace("https://discord.gg/", "discord://-/invite/");
        Protocol.Open(discord);
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        if (string.IsNullOrEmpty(Website)) return;

        Protocol.Open(Website);
    }
}