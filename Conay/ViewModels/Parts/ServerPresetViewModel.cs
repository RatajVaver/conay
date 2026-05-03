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

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _ipAddress = "Loading..";

    [ObservableProperty] private string _players = string.Empty;

    [ObservableProperty] private string _map = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIcon))]
    [NotifyPropertyChangedFor(nameof(ShowDefaultIcon))]
    private string? _icon;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowDiscord))]
    private string? _discord;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowWebsite))]
    private string? _website;

    [ObservableProperty] private bool _isFavorite;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsLegacy))] [NotifyPropertyChangedFor(nameof(IsEnhanced))]
    private string? _version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoleplay))]
    [NotifyPropertyChangedFor(nameof(IsMechPvP))]
    [NotifyPropertyChangedFor(nameof(IsDicePvP))]
    [NotifyPropertyChangedFor(nameof(IsErotic))]
    [NotifyPropertyChangedFor(nameof(HasConaySync))]
    [NotifyPropertyChangedFor(nameof(ProvidedByServerAdmins))]
    [NotifyPropertyChangedFor(nameof(ProvidedByCommunity))]
    private string[]? _tags;

    [ObservableProperty] private bool _battleEye;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModded))] [NotifyPropertyChangedFor(nameof(ModdedTooltip))]
    private int _modsCount;

    [ObservableProperty] private IBrush _playerCountColor = Brushes.White;

    [ObservableProperty] private IBrush _pingColor = Brushes.White;

    [ObservableProperty] private string _ping = "Ping: N/A";

    [ObservableProperty] private bool _refreshInProgress;

    private GameVersion EffectiveGameVersion => string.IsNullOrEmpty(Version) && _provider.GetProviderName() == "local"
        ? GameVersionHelper.Current
        : GameVersionHelper.FromPresetVersion(Version);

    public bool IsLegacy => EffectiveGameVersion == GameVersion.Legacy;
    public bool IsEnhanced => EffectiveGameVersion == GameVersion.Enhanced;

    public bool ShowIcon => !string.IsNullOrEmpty(Icon)
                            && _launcherConfig.Data is { DisplayIcons: true, OfflineMode: false };

    public bool ShowDefaultIcon => string.IsNullOrEmpty(Icon) && _launcherConfig.Data.DisplayIcons;

    public bool ShowDiscord => !string.IsNullOrEmpty(Discord);
    public bool ShowWebsite => !string.IsNullOrEmpty(Website);
    public bool IsLocalPreset => _provider.GetProviderName() == "local" && File != "_vanilla";

    public bool IsModded => ModsCount > 0;
    public bool IsRoleplay => Tags?.Contains("roleplay") ?? false;
    public bool IsMechPvP => Tags?.Contains("mech") ?? false;
    public bool IsDicePvP => Tags?.Contains("dice") ?? false;
    public bool IsErotic => Tags?.Contains("erp") ?? false;
    public bool HasConaySync => Tags?.Contains("sync") ?? false;
    public bool ProvidedByServerAdmins => !HasConaySync && _provider.GetProviderName() == "ratajmods";
    public bool ProvidedByCommunity => !HasConaySync && _provider.GetProviderName() == "github";
    public string ModdedTooltip => $"Modded ({ModsCount} mods)";

    public bool IsLoaded { get; set; }
    public bool IsDataLoaded { get; private set; }
    public bool IsVisible { get; set; }

    public readonly string File;
    private Action<ServerPresetViewModel>? _onDelete;
    private int? _queryPort;
    private int _failedQueries;

    [ObservableProperty] private int _mods;

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
            PlayerCountColor = new SolidColorBrush(Color.Parse("#888888"));
            return;
        }

        if (players >= maxPlayers - 1)
        {
            PlayerCountColor = new SolidColorBrush(Color.Parse("#db8a76"));
            return;
        }

        PlayerCountColor = Brushes.White;
    }

    private void UpdatePing(int ping)
    {
        Ping = $"Ping: {ping} ms";
        PingColor = new SolidColorBrush(new HslColor(1, Math.Max(0, 130 - ping / 2), 0.58, 0.66).ToRgb());
    }

    public async Task WarmCacheAsync()
    {
        if (IsLoaded) return;
        await Task.Run(() => _provider.FetchServerData(_serverInfo.File));
    }

    public async Task LoadDataAsync()
    {
        IsLoaded = true;
        await UpdateServerData();
    }

    private async Task UpdateServerData()
    {
        ServerData? preset = await Task.Run(() => _provider.FetchServerData(_serverInfo.File));
        if (preset != null)
        {
            IpAddress = preset.Ip;
            Discord = preset.Discord;
            Website = preset.Website;
            Version = preset.Version;
            Tags = preset.Tags;
            BattleEye = preset.BattlEye;
            ModsCount = preset.Mods.Count;
            _queryPort = preset.QueryPort;
            _preset = preset;
        }

        IsDataLoaded = true;

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

    public void SetDeleteCallback(Action<ServerPresetViewModel> onDelete) => _onDelete = onDelete;

    [RelayCommand]
    private async Task EditPreset()
    {
        ServerData? data = await _provider.FetchServerData(File);
        if (data != null)
            _router.ShowEditPreset(data);
    }

    [RelayCommand]
    private void DeletePreset() => _onDelete?.Invoke(this);

    [RelayCommand]
    private async Task LaunchServerPreset()
    {
        if (_preset != null && !_steam.DualInstallMode && EffectiveGameVersion != GameVersionHelper.Current)
        {
            string required = GameVersionHelper.ToDisplayName(_preset.GameVersion);
            string current = GameVersionHelper.ToDisplayName(GameVersionHelper.Current);
            bool confirmed = await MessageBox.Confirm(
                $"This server requires Conan Exiles {required}, but you're currently on {current}.\n\n" +
                $"You can switch branches in Steam → Library → Conan Exiles → Properties → Game Versions & Betas.\n\n" +
                $"If you want to play both Legacy and Enhanced, you may also set up a dual install in the Settings tab of Conay.\n\nLaunch anyway?");

            if (!confirmed) return;
        }

        _router.BeforeLaunch(Name);
        await _steam.WaitForSteam();
        _provider.SaveModlistFromPreset(File);
        _router.ReadyForLaunch(_preset, version: EffectiveGameVersion);
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

        Protocol.Open(Discord);
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        if (string.IsNullOrEmpty(Website)) return;

        Protocol.Open(Website);
    }
}