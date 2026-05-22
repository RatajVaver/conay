using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Utils;
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
    private readonly ModItemFactory _modItemFactory;
    private readonly LauncherConfig _launcherConfig;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _modsLoaded = "Currently loaded mods:";

    [ObservableProperty]
    private bool _launching;

    public bool ShowSupportBox { get; } = Random.Shared.NextDouble() < 0.02;

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    [ObservableProperty]
    private bool _showVersionSelector;

    public LaunchViewModel(Steam steam, ModList modList, LaunchState launchState, LaunchWorker launchWorker,
        ModItemFactory modItemFactory, LauncherConfig launcherConfig)
    {
        _steam = steam;
        _modList = modList;
        _launchState = launchState;
        _launchWorker = launchWorker;
        _modItemFactory = modItemFactory;
        _launcherConfig = launcherConfig;

        if (steam.DualInstallMode && string.IsNullOrEmpty(launchState.Name))
        {
            GameVersion? lastVersion = launcherConfig.Data.LastLaunchedVersion;
            if (lastVersion.HasValue)
                ApplyVersion(lastVersion.Value);
            else
                ShowVersionSelector = true;
        }

        Mods.CollectionChanged += (_, _) => ModsLoaded = $"Currently loaded mods ({Mods.Count}):";

        _ = LoadModlist();
        LoadLaunchData();

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    private async Task LoadModlist()
    {
        await _steam.WaitForSteam();

        if (string.IsNullOrEmpty(_launchState.Name))
            _modList.LoadModList(_launchState.Version);

        List<string> currentMods = _modList.GetCurrentModList();
        foreach (string modPath in currentMods)
        {
            Mods.Add(CreateMod(modPath));
        }
    }

    private void LoadLaunchData()
    {
        Title = !string.IsNullOrEmpty(_launchState.Name) ? _launchState.Name : "Last played modlist";
        Subtitle = _launchState.IsSaveLaunch ? "Loaded game save" : _launchState.Ip;
    }

    private ModItemViewModel CreateMod(string modPath)
    {
        ModItemViewModel vm = _modItemFactory.Create(modPath, _launchState.Version);
        vm.OnMoveUp = () => MoveMod(vm, -1);
        vm.OnMoveDown = () => MoveMod(vm, 1);
        vm.OnRemove = () => Mods.Remove(vm);
        return vm;
    }

    private void MoveMod(ModItemViewModel mod, int direction)
    {
        int index = Mods.IndexOf(mod);
        int newIndex = index + direction;
        if (newIndex < 0 || newIndex >= Mods.Count) return;
        Mods.Move(index, newIndex);
    }

    private void ApplyVersion(GameVersion version)
    {
        _launchState.Version = version;
        _modList.LoadModList(version);
    }

    [RelayCommand]
    private void SelectEnhanced()
    {
        ApplyVersion(GameVersion.Enhanced);
        ShowVersionSelector = false;
        Mods.Clear();
        ModsLoaded = "Currently loaded mods:";
        _ = LoadModlist();
        LoadLaunchData();
    }

    [RelayCommand]
    private void SelectLegacy()
    {
        ApplyVersion(GameVersion.Legacy);
        ShowVersionSelector = false;
        Mods.Clear();
        ModsLoaded = "Currently loaded mods:";
        _ = LoadModlist();
        LoadLaunchData();
    }

    [RelayCommand]
    private async Task AddMod()
    {
        Window? window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (window == null) return;
        TopLevel? topLevel = TopLevel.GetTopLevel(window);
        if (topLevel == null) return;

        IStorageFolder? startFolder = _modList.WorkshopPath != null
            ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(_modList.WorkshopPath)
            : null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select mod file(s)",
            AllowMultiple = true,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = [new FilePickerFileType("Mod files") { Patterns = ["*.pak"] }]
        });

        foreach (var file in files)
        {
            string? path = file.TryGetLocalPath();
            if (path == null) continue;

            string[] parts = path.Replace('\\', '/').Split('/');
            if (parts.Length < 2) continue;

            string modPath = $"{parts[^2]}/{parts[^1]}";
            if (Mods.Any(m => m.ModPath == modPath)) continue;

            Mods.Add(CreateMod(modPath));
        }
    }

    [RelayCommand]
    private void SaveModlist()
    {
        _modList.SaveModList([.. Mods.Select(m => m.ModPath)], _launchState.Version);
    }

    [RelayCommand]
    private static void OpenDonate() => Protocol.Open("https://ko-fi.com/rataj");

    [RelayCommand]
    private static void OpenDiscord() => Protocol.Open("https://discord.gg/3WJNxCTn8m");

    [RelayCommand]
    private void Launch()
    {
        if (Launching) return;
        Launching = true;
        _launchWorker.Launch();
    }
}