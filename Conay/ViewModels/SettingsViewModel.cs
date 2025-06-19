using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Services;
using Conay.Utils;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace Conay.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly LauncherConfig _config;
    private readonly GameConfig _gameConfig;

    [ObservableProperty]
    private bool _checkUpdates = true;

    [ObservableProperty]
    private bool _launchGame = true;

    [ObservableProperty]
    private bool _directConnect = true;

    [ObservableProperty]
    private bool _disableCinematic;

    [ObservableProperty]
    private bool _offlineMode;

    [ObservableProperty]
    private bool _displayIcons = true;

    [ObservableProperty]
    private bool _keepHistory = true;

    [ObservableProperty]
    private bool _clipboard = true;

    [ObservableProperty]
    private bool _queryServers = true;

    public SettingsViewModel(LauncherConfig config, GameConfig gameConfig)
    {
        _config = config;
        _gameConfig = gameConfig;

        CheckUpdates = config.Data.CheckUpdates;
        LaunchGame = config.Data.LaunchGame;
        DirectConnect = config.Data.DirectConnect;
        DisableCinematic = config.Data.DisableCinematic;
        OfflineMode = config.Data.OfflineMode;
        KeepHistory = config.Data.KeepHistory;
        Clipboard = config.Data.Clipboard;
        DisplayIcons = config.Data.DisplayIcons;
        QueryServers = config.Data.QueryServers;
    }

    partial void OnCheckUpdatesChanged(bool value)
    {
        if (value == _config.Data.CheckUpdates) return;
        _config.Data.CheckUpdates = value;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnLaunchGameChanged(bool value)
    {
        if (value == _config.Data.LaunchGame) return;
        _config.Data.LaunchGame = value;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnDirectConnectChanged(bool value)
    {
        if (value == _config.Data.DirectConnect) return;
        _config.Data.DirectConnect = value;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnDisableCinematicChanged(bool value)
    {
        if (value == _config.Data.DisableCinematic) return;
        _config.Data.DisableCinematic = value;
        _gameConfig.ToggleCinematicIntro(value);
        _ = _config.ScheduleConfigSave();

        IMsBox<ButtonResult> box = MessageBoxManager
            .GetMessageBoxStandard("Conay",
                value
                    ? "Cinematic intro has been disabled!\n\nYou will now see silent black screen when loading into the game."
                    : "Cinematic intro has been enabled!\n\n\"What will you do, exile?\" is back.");
        _ = box.ShowAsync();
    }

    partial void OnOfflineModeChanged(bool value)
    {
        if (value == _config.Data.OfflineMode) return;
        _config.Data.OfflineMode = value;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnDisplayIconsChanged(bool value)
    {
        if (value == _config.Data.DisplayIcons) return;
        _config.Data.DisplayIcons = value;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnKeepHistoryChanged(bool value)
    {
        if (value == _config.Data.KeepHistory) return;
        _config.Data.KeepHistory = value;

        if (!value)
        {
            _config.Data.History.Clear();
        }

        _ = _config.ScheduleConfigSave();
    }

    partial void OnClipboardChanged(bool value)
    {
        if (value == _config.Data.Clipboard) return;
        _config.Data.Clipboard = value;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnQueryServersChanged(bool value)
    {
        if (value == _config.Data.QueryServers) return;
        _config.Data.QueryServers = value;
        _ = _config.ScheduleConfigSave();
    }

    [RelayCommand]
    private static void OpenDiscord()
    {
        Protocol.Open("discord://-/invite/3WJNxCTn8m");
    }

    [RelayCommand]
    private static void OpenRepository()
    {
        Protocol.Open("https://github.com/RatajVaver/conay");
    }

    [RelayCommand]
    private static void OpenWebsite()
    {
        Protocol.Open("https://ratajmods.net/");
    }

    [RelayCommand]
    private static void OpenDonate()
    {
        Protocol.Open("https://ko-fi.com/rataj");
    }
}