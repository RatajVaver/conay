using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.Utils;
using Conay.ViewModels.Parts;
using Conay.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace Conay.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PageFactory? _pageFactory;

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

    private readonly Steam? _steam;
    private readonly LaunchState? _launchState;
    private readonly LauncherConfig? _launcherConfig;
    private readonly SelfUpdater? _selfUpdater;
    private ModList? _modList;

    public bool IsLaunchPageActive => CurrentPage is LaunchViewModel;
    public bool IsFavoritePageActive => CurrentPage is FavoriteViewModel;
    public bool IsServersPageActive => CurrentPage is ServersViewModel;
    public bool IsPresetsPageActive => CurrentPage is PresetsViewModel;
    public bool IsSettingsPageActive => CurrentPage is SettingsViewModel;

    public MainViewModel()
    {
        _currentPage = new PageViewModel(); // fallback for designer
    }

    public MainViewModel(PageFactory pageFactory, LaunchState launchState, LaunchWorker launchWorker, LauncherConfig
        launcherConfig, SelfUpdater selfUpdater, Steam steam, ModList modList, ModSourceFactory modSourceFactory)
    {
        _pageFactory = pageFactory;
        _launchState = launchState;
        _launcherConfig = launcherConfig;
        _selfUpdater = selfUpdater;
        _steam = steam;
        _modList = modList;

        launchWorker.StatusChanged += OnStatusChanged;

        _selfUpdater.StatusChanged += OnStatusChanged;
        _selfUpdater.DownloadProgressChanged += OnModDownloadProgressChanged;

        _steam.StatusChanged += OnStatusChanged;
        _steam.DownloadProgressChanged += OnModDownloadProgressChanged;

        modSourceFactory.StatusChanged += OnStatusChanged;
        modSourceFactory.DownloadProgressChanged += OnModDownloadProgressChanged;

        IsMenuCollapsed = launcherConfig.Data.MenuCollapsed;

        ShowServers();

        if (_launcherConfig.Data.OfflineMode)
        {
            StatusText = "Conay is in offline mode, mods will not be updated.";
        }
        else
        {
            _ = RunUpdates();
        }
    }

    private void OnStatusChanged(object? sender, string status)
    {
        StatusText = status;
    }

    private void OnModDownloadProgressChanged(object? sender, double progress)
    {
        ProgressBarValue = progress;
    }

    private async Task RunUpdates()
    {
        await CheckSelfUpdate();

        if (_steam == null)
            return;

        await _steam.CheckSubscribedModUpdates();
    }

    private async Task CheckSelfUpdate()
    {
        if (_selfUpdater == null || _launcherConfig == null)
            return;

        if (_launcherConfig.Data is not { OfflineMode: false, CheckUpdates: true })
            return;

        StatusText = "Checking Conay update..";

        bool foundUpdate = await SelfUpdater.CheckUpdate();
        if (!foundUpdate)
            return;

        IMsBox<ButtonResult> box = MessageBoxManager
            .GetMessageBoxStandard("Conay",
                "There's a new update available for Conay!\nWould you like to download it now?", ButtonEnum.YesNo);
        ButtonResult result = await box.ShowAsync();

        if (result.Equals(ButtonResult.Yes))
        {
            await _selfUpdater.DownloadInstaller();
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

    public void ShowLaunchForPreset(ServerData? preset)
    {
        if (_launchState != null)
        {
            _launchState.Name = preset?.Name ?? string.Empty;
            _launchState.Ip = preset?.Ip ?? string.Empty;
            _launchState.Password = preset?.Password ?? string.Empty;
        }

        CurrentPage = _pageFactory!.GetPageViewModel<LaunchViewModel>(lvm => { lvm.LaunchCommand.Execute(null); });
    }
}