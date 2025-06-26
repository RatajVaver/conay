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
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;

namespace Conay.ViewModels;

public partial class PresetsViewModel : PageViewModel
{
    private readonly ServerPresetFactory _presetFactory;
    private readonly ServerList _serverList;
    private readonly ModList _modList;

    public bool ListIsEmpty => Presets.Count == 0;

    public ObservableCollection<ServerPresetViewModel> Presets { get; } = [];

    public PresetsViewModel(ServerPresetFactory presetFactory, ServerList serverList, ModList modList)
    {
        _presetFactory = presetFactory;
        _serverList = serverList;
        _modList = modList;

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
            Presets.Add(_presetFactory.Create(server));
        }
    }

    [RelayCommand]
    private void AddPreset()
    {
        string fileName = _modList.CreatePresetFromCurrentModList();

        IMsBox<ButtonResult> box = MessageBoxManager
            .GetMessageBoxStandard("Conay",
                $"New preset '{fileName}' was created in the servers folder.\n" +
                "Make sure to rename it and fill in any other details.\n" +
                "The preset will appear in this list with next launch of Conay.");
        _ = box.ShowAsync();

        string appDirectory = AppContext.BaseDirectory;
        string filePath = Path.GetFullPath(Path.Combine(appDirectory, "servers", fileName));

        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
    }

    [RelayCommand]
    private static void OpenPresetsFolder()
    {
        string appDirectory = AppContext.BaseDirectory;
        string directoryPath = Path.GetFullPath(Path.Combine(appDirectory, "servers"));

        Process.Start("explorer.exe", directoryPath);
    }
}