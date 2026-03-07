using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;
using Conay.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Conay.ViewModels;

public partial class PresetsViewModel : PageViewModel
{
    private readonly ServerPresetFactory _presetFactory;
    private readonly ServerList _serverList;
    private readonly LocalPresets _localPresets;
    private readonly Router _router;

    public bool ListIsEmpty => Presets.Count == 0;

    public ObservableCollection<ServerPresetViewModel> Presets { get; } = [];

    public PresetsViewModel(ServerPresetFactory presetFactory, ServerList serverList, LocalPresets localPresets, Router router)
    {
        _presetFactory = presetFactory;
        _serverList = serverList;
        _localPresets = localPresets;
        _router = router;

        Presets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ListIsEmpty));

        _ = LoadLocalServers();

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    private async Task LoadLocalServers()
    {
        RefreshServers();

        while (!_serverList.LocalServersLoaded)
        {
            await Task.Delay(50);
            RefreshServers();
        }
    }

    private void RefreshServers()
    {
        Presets.Clear();

        List<ServerInfo> servers = _serverList.GetLocalServers();
        foreach (ServerInfo server in servers)
        {
            ServerPresetViewModel preset = _presetFactory.Create(server);
            if (preset.IsLocalPreset)
                preset.SetDeleteCallback(vm => _ = DeletePresetAsync(vm));
            Presets.Add(preset);
        }
    }

    private async Task DeletePresetAsync(ServerPresetViewModel preset)
    {
        ButtonResult result = await MessageBoxManager.GetMessageBoxStandard("Conay",
            $"Are you sure you want to delete \"{preset.Name}\"? This cannot be undone.",
            ButtonEnum.YesNo).ShowAsync();

        if (result != ButtonResult.Yes) return;

        _localPresets.DeletePreset(preset.File);
        _presetFactory.Invalidate(preset.File);
        await _serverList.RefreshLocalServers();
        RefreshServers();
    }

    [RelayCommand]
    private void AddPreset() => _router.ShowAddPreset();

    [RelayCommand]
    private static void OpenPresetsFolder()
    {
        string appDirectory = AppContext.BaseDirectory;
        string directoryPath = Path.GetFullPath(Path.Combine(appDirectory, "servers"));

        Process.Start("explorer.exe", directoryPath);
    }
}