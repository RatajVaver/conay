using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Conay.Services;
using Conay.Utils;

namespace Conay.ViewModels;

public class AlternativeBordersChangedMessage(bool value) : ValueChangedMessage<bool>(value);

public partial class SettingsViewModel : PageViewModel
{
    private readonly LauncherConfig _config;
    private readonly GameConfig _gameConfig;
    private readonly Steam _steam;

    [ObservableProperty]
    private bool _checkUpdates = true;

    [ObservableProperty]
    private bool _updateSubscribedModsOnLaunch = true;

    [ObservableProperty]
    private bool _automaticallySubscribe;

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
    private bool _useCache = true;

    [ObservableProperty]
    private int _defaultTabIndex = 2;

    [ObservableProperty]
    private bool _keepHistory = true;

    [ObservableProperty]
    private bool _clipboard = true;

    [ObservableProperty]
    private bool _queryServers = true;

    [ObservableProperty]
    private bool _backupTotCustom;

    [ObservableProperty]
    private bool _alternativeBorders;

    private readonly string[] _tabs = ["launch", "favorite", "servers", "presets", "saves"];

    public SettingsViewModel(LauncherConfig config, GameConfig gameConfig, Steam steam)
    {
        _config = config;
        _gameConfig = gameConfig;
        _steam = steam;

        CheckUpdates = config.Data.CheckUpdates;
        UpdateSubscribedModsOnLaunch = config.Data.UpdateSubscribedModsOnLaunch;
        AutomaticallySubscribe = config.Data.AutomaticallySubscribe;
        LaunchGame = config.Data.LaunchGame;
        DirectConnect = config.Data.DirectConnect;
        DisableCinematic = config.Data.DisableCinematic;
        ImmersiveMode = config.Data.ImmersiveMode;
        OfflineMode = config.Data.OfflineMode;
        KeepHistory = config.Data.KeepHistory;
        Clipboard = config.Data.Clipboard;
        DisplayIcons = config.Data.DisplayIcons;
        UseCache = config.Data.UseCache;
        QueryServers = config.Data.QueryServers;
        BackupTotCustom = config.Data.BackupTotCustom;
        AlternativeBorders = config.Data.AlternativeBorders;

        int tabIndex = Array.IndexOf(_tabs, config.Data.DefaultTab);
        if (tabIndex == -1) tabIndex = 2;
        DefaultTabIndex = tabIndex;
    }

    private void UpdateConfig<T>(T currentValue, T newValue, Action<T> updateConfig)
    {
        if (Equals(currentValue, newValue)) return;
        updateConfig(newValue);
        _ = _config.ScheduleConfigSave();
    }

    partial void OnCheckUpdatesChanged(bool value) =>
        UpdateConfig(_config.Data.CheckUpdates, value,
            v => _config.Data.CheckUpdates = v);

    partial void OnUpdateSubscribedModsOnLaunchChanged(bool value) =>
        UpdateConfig(_config.Data.UpdateSubscribedModsOnLaunch, value,
            v => _config.Data.UpdateSubscribedModsOnLaunch = v);

    partial void OnAutomaticallySubscribeChanged(bool value) =>
        UpdateConfig(_config.Data.AutomaticallySubscribe, value,
            v => _config.Data.AutomaticallySubscribe = v);

    partial void OnLaunchGameChanged(bool value) =>
        UpdateConfig(_config.Data.LaunchGame, value,
            v => _config.Data.LaunchGame = v);

    partial void OnDirectConnectChanged(bool value) =>
        UpdateConfig(_config.Data.DirectConnect, value,
            v => _config.Data.DirectConnect = v);

    partial void OnOfflineModeChanged(bool value) =>
        UpdateConfig(_config.Data.OfflineMode, value,
            v => _config.Data.OfflineMode = v);

    partial void OnDisplayIconsChanged(bool value) =>
        UpdateConfig(_config.Data.DisplayIcons, value,
            v => _config.Data.DisplayIcons = v);

    partial void OnClipboardChanged(bool value) =>
        UpdateConfig(_config.Data.Clipboard, value,
            v => _config.Data.Clipboard = v);

    partial void OnQueryServersChanged(bool value) =>
        UpdateConfig(_config.Data.QueryServers, value,
            v => _config.Data.QueryServers = v);

    partial void OnDisableCinematicChanged(bool value)
    {
        if (value == _config.Data.DisableCinematic) return;

        bool success = _gameConfig.ToggleCinematicIntro(value);
        if (success)
        {
            _config.Data.DisableCinematic = value;
            _ = _config.ScheduleConfigSave();

            MessageBox.ShowInfo(value
                ? "Cinematic intro has been disabled!\n\n" +
                  "You will now see silent black screen when loading into the game."
                : "Cinematic intro has been enabled!\n\n" +
                  "\"What will you do, exile?\" is back.");
        }
        else
        {
            MessageBox.ShowInfo(
                "Failed to save the config! Make sure you have write permissions to the game's folder.");
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

            MessageBox.ShowInfo(value
                ? "Immersive mode has been enabled!\n\nRecommended changes were saved into the game settings."
                : "Immersive mode has been disabled!\n\nAffected settings have been restored to default values.");
        }
        else
        {
            MessageBox.ShowInfo("Failed to save the config! Make sure you have write permissions to the game folder.");
        }
    }

    partial void OnUseCacheChanged(bool value)
    {
        if (value == _config.Data.UseCache) return;
        _config.Data.UseCache = value;

        if (!value)
        {
            _config.ClearCache();
        }

        _ = _config.ScheduleConfigSave();
    }

    partial void OnDefaultTabIndexChanged(int value)
    {
        if (value < 0 || value >= _tabs.Length) return;
        string tabName = _tabs[value];

        if (tabName == _config.Data.DefaultTab) return;
        _config.Data.DefaultTab = tabName;
        _ = _config.ScheduleConfigSave();
    }

    partial void OnBackupTotCustomChanged(bool value) =>
        UpdateConfig(_config.Data.BackupTotCustom, value,
            v => _config.Data.BackupTotCustom = v);

    partial void OnAlternativeBordersChanged(bool value)
    {
        UpdateConfig(_config.Data.AlternativeBorders, value, v => _config.Data.AlternativeBorders = v);
        WeakReferenceMessenger.Default.Send(new AlternativeBordersChangedMessage(value));
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

    public bool DualInstallActive => _steam.DualInstallMode;
    public bool DualInstallNotActive => !_steam.DualInstallMode;

    [RelayCommand]
    private async System.Threading.Tasks.Task OpenDualInstallWizard()
    {
        if (!_steam.DualInstallMode && string.IsNullOrEmpty(_steam.AppInstallDir))
        {
            MessageBox.ShowInfo("Steam is not connected yet.\n\nPlease wait for Steam to load and try again.");
            return;
        }

        Views.DualInstallWizardView wizard = new()
        {
            DataContext = new DualInstallWizardViewModel(_steam)
        };

        Avalonia.Controls.Window? owner = (Avalonia.Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner != null)
        {
            await wizard.ShowDialog(owner);
            return;
        }

        wizard.Show();
    }

    [RelayCommand]
    private static void OpenDiscord() => Protocol.Open("https://discord.gg/3WJNxCTn8m");

    [RelayCommand]
    private static void OpenRepository() => Protocol.Open("https://github.com/RatajVaver/conay");

    [RelayCommand]
    private static void OpenWebsite() => Protocol.Open("https://ratajmods.net/");

    [RelayCommand]
    private static void OpenDonate() => Protocol.Open("https://ko-fi.com/rataj");
}