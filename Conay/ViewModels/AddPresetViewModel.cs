using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
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

    private string _originalFileName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _ip;

    [ObservableProperty]
    private string _password = string.Empty;

    public string ModsLabel => $"Mods in preset ({Mods.Count}):";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnhancedSelected), nameof(IsLegacySelected))]
    private GameVersion _selectedVersion;

    public bool IsEnhancedSelected
    {
        get => SelectedVersion == GameVersion.Enhanced;
        set
        {
            if (value) SelectedVersion = GameVersion.Enhanced;
        }
    }

    public bool IsLegacySelected
    {
        get => SelectedVersion == GameVersion.Legacy;
        set
        {
            if (value) SelectedVersion = GameVersion.Legacy;
        }
    }

    [ObservableProperty]
    private string _title = "New preset";

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    public AddPresetViewModel(LocalPresets localPresets, ServerList serverList, ServerPresetFactory serverPresetFactory,
        ModList modList, ModItemFactory modItemFactory, GameConfig gameConfig, Router router,
        LauncherConfig launcherConfig)
    {
        _localPresets = localPresets;
        _serverList = serverList;
        _serverPresetFactory = serverPresetFactory;
        _modItemFactory = modItemFactory;
        _modList = modList;
        _router = router;
        _selectedVersion = launcherConfig.Data.LastLaunchedVersion ?? GameVersion.Enhanced;
        _ip = gameConfig.GetLastConnected(_selectedVersion);
        Mods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ModsLabel));

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    public void LoadCurrentModlist()
    {
        Mods.Clear();
        foreach (string modPath in _modList.ReloadCurrentModListForVersion(SelectedVersion))
            Mods.Add(CreateMod(modPath));
    }

    public void Prefill(ServerData preset)
    {
        _originalFileName = preset.FileName ?? string.Empty;
        FileName = _originalFileName;
        Name = preset.Name;
        Ip = preset.Ip;
        Password = preset.Password ?? string.Empty;
        SelectedVersion = preset.GameVersion;
        Title = "Edit preset";

        Mods.Clear();
        foreach (string mod in preset.Mods)
            Mods.Add(CreateMod(mod));
    }

    private ModItemViewModel CreateMod(string modPath)
    {
        ModItemViewModel vm = _modItemFactory.Create(modPath, SelectedVersion);
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

    [RelayCommand]
    private void CopyFromCurrentModlist() => LoadCurrentModlist();

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

        if (!string.IsNullOrEmpty(_originalFileName) && _originalFileName != fileName)
        {
            _localPresets.DeletePreset(_originalFileName);
            _serverPresetFactory.Invalidate(_originalFileName);
        }

        _serverPresetFactory.Invalidate(fileName);
        await _serverList.RefreshLocalServers();
        _router.ShowPresets();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(FileName);

    [RelayCommand]
    private void Cancel() => _router.ShowPresets();
}