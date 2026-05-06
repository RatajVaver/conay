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
    private readonly ModItemFactory _modItemFactory;
    private readonly ModList _modList;
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

    [ObservableProperty]
    private string _modsLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnhancedSelected), nameof(IsLegacySelected))]
    private GameVersion _selectedVersion = GameVersion.Enhanced;

    public bool IsEnhancedSelected
    {
        get => SelectedVersion == GameVersion.Enhanced;
        set { if (value) SelectedVersion = GameVersion.Enhanced; }
    }

    public bool IsLegacySelected
    {
        get => SelectedVersion == GameVersion.Legacy;
        set { if (value) SelectedVersion = GameVersion.Legacy; }
    }

    [ObservableProperty]
    private string _title = "New preset";

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    public AddPresetViewModel(LocalPresets localPresets, ServerList serverList, ServerPresetFactory serverPresetFactory,
        ModList modList, ModItemFactory modItemFactory, GameConfig gameConfig, Router router)
    {
        _localPresets = localPresets;
        _serverList = serverList;
        _serverPresetFactory = serverPresetFactory;
        _modItemFactory = modItemFactory;
        _modList = modList;
        _router = router;
        _ip = gameConfig.GetLastConnected();

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    public void LoadCurrentModlist()
    {
        Mods.Clear();
        foreach (string modPath in _modList.ReloadCurrentModList())
            Mods.Add(_modItemFactory.Create(modPath));

        ModsLabel = $"Mods from current modlist ({Mods.Count}):";
    }

    public void Prefill(ServerData preset)
    {
        FileName = preset.FileName ?? string.Empty;
        Name = preset.Name;
        Ip = preset.Ip;
        Password = preset.Password ?? string.Empty;
        SelectedVersion = preset.GameVersion;
        Title = "Edit preset";

        Mods.Clear();
        foreach (string mod in preset.Mods)
            Mods.Add(_modItemFactory.Create(mod));

        ModsLabel = $"Mods from preset ({Mods.Count}):";
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
            Version = SelectedVersion == GameVersion.Enhanced ? "enhanced" : "legacy",
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