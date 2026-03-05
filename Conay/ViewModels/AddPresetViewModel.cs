using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using System.Threading.Tasks;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;
using Conay.Views;

namespace Conay.ViewModels;

public partial class AddPresetViewModel : PageViewModel
{
    private readonly LocalPresets _localPresets;
    private readonly ServerList _serverList;
    private readonly ServerPresetFactory _serverPresetFactory;
    private readonly Router _router;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _ip = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public string ModsLabel { get; }
    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    public AddPresetViewModel(LocalPresets localPresets, ServerList serverList, ServerPresetFactory serverPresetFactory,
        ModList modList, ModItemFactory modItemFactory, Router router)
    {
        _localPresets = localPresets;
        _serverList = serverList;
        _serverPresetFactory = serverPresetFactory;
        _router = router;

        List<string> currentMods = modList.ReloadCurrentModList();
        foreach (string modPath in currentMods)
            Mods.Add(modItemFactory.Create(modPath));

        ModsLabel = $"Mods from current modlist ({Mods.Count}):";

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        string fileName = FileName.Trim();
        ServerData data = new()
        {
            Name = string.IsNullOrWhiteSpace(Name) ? fileName : Name.Trim(),
            Ip = Ip.Trim(),
            Password = string.IsNullOrWhiteSpace(Password) ? null : Password.Trim(),
            Mods = [.. Mods.Select(m => m.ModPath)]
        };
        _localPresets.SavePreset(fileName, data);
        _serverPresetFactory.Invalidate(fileName);
        await _serverList.RefreshLocalServers();
        _router.ShowPresets();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(FileName);

    [RelayCommand]
    private void Cancel() => _router.ShowPresets();
}