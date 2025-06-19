using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Data;
using Conay.Services;
using Conay.Utils;

namespace Conay.ViewModels.Parts;

public partial class ServerPresetViewModel : ViewModelBase
{
    private readonly Router _router;
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

    public bool ShowIcon => !string.IsNullOrEmpty(Icon)
                            && _launcherConfig.Data is { DisplayIcons: true, OfflineMode: false };

    public bool ShowDiscord => !string.IsNullOrEmpty(Discord);
    public bool ShowWebsite => !string.IsNullOrEmpty(Website);

    public bool IsModded => ModsCount > 0;
    public bool IsRoleplay => Tags?.Contains("roleplay") ?? false;
    public bool IsMechPvP => Tags?.Contains("mech") ?? false;
    public bool IsDicePvP => Tags?.Contains("dice") ?? false;
    public bool HasConaySync => Tags?.Contains("sync") ?? false;
    public string ModdedTooltip => $"Modded ({ModsCount} mods)";

    public readonly string File;
    private int? _queryPort;

    [ObservableProperty]
    private int _mods;

    private readonly ServerInfo _serverInfo;
    private ServerData? _preset;

    public ServerPresetViewModel(Router router, LauncherConfig launcherConfig, ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
        _router = router;
        _launcherConfig = launcherConfig;
        _provider = serverInfo.Provider!;

        Name = serverInfo.Name;
        Icon = serverInfo.Icon;
        File = serverInfo.File;
        IsFavorite = launcherConfig.IsServerFavorite(File);

        _ = UpdateServerData();
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

    private async Task GetServerOnlineStatus()
    {
        if (_queryPort == null) return;

        ServerQueryResult result = await Steam.QueryServer(IpAddress.Split(':')[0], (int)_queryPort);
        if (result.MaxPlayers <= 0) return;

        Players = $"{result.Players} / {result.MaxPlayers}";
        Map = result.Map;
    }

    [RelayCommand]
    private void LaunchServerPreset()
    {
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