using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;
using Conay.Views;

namespace Conay.ViewModels;

public partial class LaunchViewModel : PageViewModel
{
    private readonly Steam _steam;
    private readonly ModList _modList;
    private readonly LaunchState _launchState;
    private readonly LaunchWorker _launchWorker;
    private readonly LauncherConfig _launcherConfig;
    private readonly GameConfig _gameConfig;
    private readonly ModItemFactory _modItemFactory;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _modsLoaded = "Currently loaded mods:";

    [ObservableProperty]
    private bool _launching;

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    public LaunchViewModel(Steam steam, ModList modList, LaunchState launchState, LaunchWorker launchWorker,
        LauncherConfig launcherConfig, GameConfig gameConfig, ModItemFactory modItemFactory)
    {
        _steam = steam;
        _modList = modList;
        _launchState = launchState;
        _launchWorker = launchWorker;
        _launcherConfig = launcherConfig;
        _gameConfig = gameConfig;
        _modItemFactory = modItemFactory;

        _ = LoadModlist();
        LoadLaunchData();

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    private async Task LoadModlist()
    {
        await _steam.WaitForSteam();

        List<string> currentMods = _modList.GetCurrentModList();
        foreach (string modPath in currentMods)
        {
            Mods.Add(_modItemFactory.Create(modPath));
        }

        ModsLoaded = $"Currently loaded mods ({Mods.Count}):";
    }

    private void LoadLaunchData()
    {
        Title = !string.IsNullOrEmpty(_launchState.Name) ? _launchState.Name : "Last played modlist";
        Subtitle = !string.IsNullOrEmpty(_launchState.Ip) ? _launchState.Ip : _gameConfig.GetLastConnected();
    }

    [RelayCommand]
    private void Launch()
    {
        if (Launching) return;
        Launching = true;
        _launchWorker.Launch();
    }
}