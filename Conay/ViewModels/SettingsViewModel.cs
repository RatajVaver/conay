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
    private bool _updateSubscribedModsOnLaunch = true;

    [ObservableProperty]
    private bool _launchGame = true;

    [ObservableProperty]
    private bool _directConnect = true;

    [ObservableProperty]
    private bool _disableCinematic;

    [ObservableProperty]
    private bool _immersiveMode;

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
        UpdateSubscribedModsOnLaunch = config.Data.UpdateSubscribedModsOnLaunch;
        LaunchGame = config.Data.LaunchGame;
        DirectConnect = config.Data.DirectConnect;
        DisableCinematic = config.Data.DisableCinematic;
        ImmersiveMode = config.Data.ImmersiveMode;
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

    partial void OnUpdateSubscribedModsOnLaunchChanged(bool value)
    {
        if (value == _config.Data.UpdateSubscribedModsOnLaunch) return;
        _config.Data.UpdateSubscribedModsOnLaunch = value;
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

        bool success = _gameConfig.ToggleCinematicIntro(value);
        if (success)
        {
            _config.Data.DisableCinematic = value;
            _ = _config.ScheduleConfigSave();

            IMsBox<ButtonResult> box = MessageBoxManager
                .GetMessageBoxStandard("Conay",
                    value
                        ? "Cinematic intro has been disabled!\n\nYou will now see silent black screen when loading into the game."
                        : "Cinematic intro has been enabled!\n\n\"What will you do, exile?\" is back.");
            _ = box.ShowAsync();
        }
        else
        {
            IMsBox<ButtonResult> box = MessageBoxManager
                .GetMessageBoxStandard("Conay",
                    "Failed to save the config! Make sure you have write permissions to the game's folder.");
            _ = box.ShowAsync();
        }
    }

    partial void OnImmersiveModeChanged(bool value)
    {
        if (value == _config.Data.ImmersiveMode) return;

        bool success = _gameConfig.ToggleImmersiveMode(value);
        if (success)
        {
            _config.Data.ImmersiveMode = value;
            _ = _config.ScheduleConfigSave();

            IMsBox<ButtonResult> box = MessageBoxManager
                .GetMessageBoxStandard("Conay",
                    value
                        ? "Immersive mode has been enabled!\n\nRecommended changes were saved into the game settings."
                        : "Immersive mode has been disabled!\n\nAffected settings have been restored to default values.");
            _ = box.ShowAsync();
        }
        else
        {
            IMsBox<ButtonResult> box = MessageBoxManager
                .GetMessageBoxStandard("Conay",
                    "Failed to save the config! Make sure you have write permissions to the game folder.");
            _ = box.ShowAsync();
        }
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