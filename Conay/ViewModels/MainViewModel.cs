using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.Utils;
using Conay.ViewModels.Parts;
using Conay.Views;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace Conay.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PageFactory? _pageFactory;
    private readonly Steam? _steam;
    private readonly LaunchState? _launchState;
    private readonly LauncherConfig? _launcherConfig;
    private readonly SelfUpdater? _selfUpdater;
    private readonly PresetSourceFactory? _presetSourceFactory;
    private readonly ServerPresetFactory? _serverPresetFactory;
    private readonly ILogger<MainViewModel>? _logger;

    [ObservableProperty]
    private bool _isMenuCollapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLaunchPageActive))]
    [NotifyPropertyChangedFor(nameof(IsFavoritePageActive))]
    [NotifyPropertyChangedFor(nameof(IsServersPageActive))]
    [NotifyPropertyChangedFor(nameof(IsPresetsPageActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPageActive))]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private string _statusText = "Loading..";

    public static bool ShowTestingWarning => Meta.GetVersion().Contains('-');

    [ObservableProperty]
    private double _progressBarValue;

    public bool IsLaunchPageActive => CurrentPage is LaunchViewModel;
    public bool IsFavoritePageActive => CurrentPage is FavoriteViewModel;
    public bool IsServersPageActive => CurrentPage is ServersViewModel;
    public bool IsPresetsPageActive => CurrentPage is PresetsViewModel;
    public bool IsSettingsPageActive => CurrentPage is SettingsViewModel;

    public MainViewModel()
    {
        _currentPage = new PageViewModel(); // fallback for designer
    }

    public MainViewModel(PageFactory pageFactory, LaunchState launchState, LauncherConfig launcherConfig,
        SelfUpdater selfUpdater, Steam steam, NotifyService notifyService, PresetSourceFactory presetSourceFactory,
        ServerPresetFactory serverPresetFactory, Router router, ILogger<MainViewModel> logger)
    {
        _pageFactory = pageFactory;
        _launchState = launchState;
        _launcherConfig = launcherConfig;
        _selfUpdater = selfUpdater;
        _steam = steam;
        _logger = logger;
        _presetSourceFactory = presetSourceFactory;
        _serverPresetFactory = serverPresetFactory;

        notifyService.StatusChanged += OnStatusChanged;
        notifyService.DownloadProgressChanged += OnModDownloadProgressChanged;

        router.OnBeforeLaunch += BeforeLaunch;
        router.ShowLaunchForPreset += ShowLaunchForPreset;

        IsMenuCollapsed = launcherConfig.Data.MenuCollapsed;

        switch (launcherConfig.Data.DefaultTab)
        {
            case "launch": ShowLaunch(); break;
            case "favorite": ShowFavorite(); break;
            case "presets": ShowPresets(); break;
            default: ShowServers(); break;
        }

        if (_launcherConfig.Data.OfflineMode)
        {
            StatusText = "Conay is in offline mode, mods will not be updated.";
        }
        else
        {
            _ = RunUpdates();

            if (_launcherConfig.Data.QueryServers)
            {
                DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
                refreshTimer.Tick += (_, _) => RefreshVisibleServers();
                refreshTimer.Start();
            }
        }

        _ = CheckStartupArguments();
    }

    private void OnStatusChanged(object? sender, string status)
    {
        StatusText = status;
    }

    private void OnModDownloadProgressChanged(object? sender, double progress)
    {
        ProgressBarValue = progress;
    }

    private async Task CheckStartupArguments()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int serverArgIndex = Array.FindIndex(arguments, x => x is "--server" or "-s");
        string server = serverArgIndex >= 0 && serverArgIndex < arguments.Length - 1
            ? arguments[serverArgIndex + 1]
            : string.Empty;

        if (server.Length > 0)
        {
            _logger?.LogDebug("Server preset argument: {Server}", server);

            foreach (string source in new[] { "local", "ratajmods", "github" })
            {
                IPresetService provider = _presetSourceFactory!.Get(source);
                ServerData? serverData = await provider.FetchServerData(server);
                if (serverData == null) continue;
                BeforeLaunch(serverData.Name);
                await _steam!.WaitForSteam();
                provider.SaveModlistFromPreset(server);
                _launcherConfig?.SaveIntoHistory(server);
                ShowLaunchForPreset(serverData);
                return;
            }

            StatusText = $"Server preset '{server}' not found!";
        }
    }

    private async Task RunUpdates()
    {
        await CheckSelfUpdate();

        if (_steam == null)
            return;

        if (_launcherConfig?.Data.UpdateSubscribedModsOnLaunch ?? false)
        {
            await _steam.CheckSubscribedModUpdates();
        }
        else if (StatusText == "Loading..")
        {
            StatusText = "";
        }
    }

    private async Task CheckSelfUpdate()
    {
        if (_selfUpdater == null || _launcherConfig == null)
            return;

        if (_launcherConfig.Data is not { OfflineMode: false, CheckUpdates: true })
            return;

        StatusText = "Checking Conay updates..";

        bool foundUpdate = await _selfUpdater.CheckUpdate();
        if (!foundUpdate)
        {
            if (StatusText == "Checking Conay updates..")
            {
                StatusText = "";
            }

            return;
        }

        StatusText = "New Conay update available!";

        IMsBox<ButtonResult> box = MessageBoxManager
            .GetMessageBoxStandard("Conay",
                "There's a new update available for Conay!\nWould you like to download it now?", ButtonEnum.YesNo);
        ButtonResult result = await box.ShowAsync();

        if (result.Equals(ButtonResult.Yes))
        {
            await _selfUpdater.DownloadInstaller();
        }
        else if (StatusText == "New Conay update available!")
        {
            StatusText = "";
        }
    }

    [RelayCommand]
    private void ToggleMenuCollapse()
    {
        IsMenuCollapsed = !IsMenuCollapsed;

        if (_launcherConfig == null || _launcherConfig.Data.MenuCollapsed == IsMenuCollapsed) return;

        _launcherConfig.Data.MenuCollapsed = IsMenuCollapsed;
        _ = _launcherConfig.ScheduleConfigSave();
    }

    [RelayCommand]
    private void ShowLaunch() => CurrentPage = _pageFactory!.GetPageViewModel<LaunchViewModel>();

    [RelayCommand]
    private void ShowFavorite() => CurrentPage = _pageFactory!.GetPageViewModel<FavoriteViewModel>(fvm =>
    {
        fvm.RefreshServers();
    });

    [RelayCommand]
    private void ShowServers() => CurrentPage = _pageFactory!.GetPageViewModel<ServersViewModel>(svm =>
    {
        foreach (ServerPresetViewModel server in svm.Presets)
        {
            server.IsFavorite = _launcherConfig!.IsServerFavorite(server.File);
        }

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    });

    [RelayCommand]
    private void ShowPresets() => CurrentPage = _pageFactory!.GetPageViewModel<PresetsViewModel>();

    [RelayCommand]
    private void ShowSettings() => CurrentPage = _pageFactory!.GetPageViewModel<SettingsViewModel>();

    private void BeforeLaunch(string? name = null)
    {
        StatusText = !string.IsNullOrEmpty(name) ? $"Launching {name}.." : "Launching..";
    }

    private void ShowLaunchForPreset(ServerData? preset)
    {
        if (_launchState != null)
        {
            _launchState.Name = preset?.Name ?? string.Empty;
            _launchState.Ip = preset?.Ip ?? string.Empty;
            _launchState.Password = preset?.Password ?? string.Empty;
        }

        CurrentPage = _pageFactory!.GetPageViewModel<LaunchViewModel>(lvm => { lvm.LaunchCommand.Execute(null); });
    }

    private void RefreshVisibleServers()
    {
        List<ServerPresetViewModel> serverPresets = _serverPresetFactory!.GetAll();
        foreach (ServerPresetViewModel preset in serverPresets.Where(preset => preset.IsVisible))
        {
            _ = preset.GetServerOnlineStatus();
        }
    }
}