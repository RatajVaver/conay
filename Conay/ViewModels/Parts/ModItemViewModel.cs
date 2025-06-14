using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.Utils;

namespace Conay.ViewModels.Parts;

public partial class ModItemViewModel : ViewModelBase
{
    private readonly Steam _steam;
    private readonly LauncherConfig _launcherConfig;
    private readonly string _localFolder = string.Empty;
    private readonly ulong? _modId;
    private readonly string _pakName;

    public readonly string ModPath;

    [ObservableProperty]
    private bool _isWorkshopMod;

    private readonly IModSource? _modProvider;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _updated = string.Empty;

    [ObservableProperty]
    private string _size = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIcon))]
    private string? _icon;

    public bool ShowIcon => !string.IsNullOrEmpty(Icon)
                            && _launcherConfig.Data is { DisplayIcons: true, OfflineMode: false };

    public ModItemViewModel(Steam steam, LauncherConfig launcherConfig, ModSourceFactory modSourceFactory,
        string modPath)
    {
        _steam = steam;
        _launcherConfig = launcherConfig;
        _title = modPath.Replace(".pak", "");
        ModPath = modPath;
        _pakName = Path.GetFileNameWithoutExtension(modPath);

        string[] parts = modPath.Split('/');
        if (ulong.TryParse(parts[0], out ulong modId))
        {
            _modId = modId;
            IsWorkshopMod = true;
            _modProvider = steam;
        }
        else
        {
            _localFolder = parts[0];
            IsWorkshopMod = false;

            string providerName = parts[0][1..];
            if (modSourceFactory.IsKnownSource(providerName))
            {
                _modProvider = modSourceFactory.Get(providerName);
            }
        }

        _ = UpdateModData();
    }

    private async Task UpdateModData()
    {
        if (_launcherConfig.Data.OfflineMode)
            return;

        if (IsWorkshopMod && _modId.HasValue)
        {
            ModInfo? data = await _steam.GetModData((ulong)_modId);
            if (data == null) return;

            Title = data.Title;
            Size = data.Size + " MB";
            Icon = data.Icon;
            Updated = "updated " + HumanReadable.TimeAgo(data.LastUpdate);

            return;
        }

        if (!IsWorkshopMod)
        {
            if (_modProvider != null && _modProvider.GetType() == typeof(WebSync))
            {
                WebSync provider = (WebSync)_modProvider;
                ExternalModInfo? data = await provider.GetModInfo(_pakName);
                if (data == null) return;

                Title = data.Title ?? _pakName;
                if (data.Size != null)
                    Size = Math.Ceiling((double)data.Size / 1024 / 1024) + " MB";
                Icon = data.Icon;
                Updated = "updated " + HumanReadable.TimeAgo(Epoch.ToDateTime(data.LastUpdate));
            }
        }
    }

    [RelayCommand]
    private void OpenWorkshopPage()
    {
        if (IsWorkshopMod && _modId.HasValue)
        {
            Steam.OpenWorkshopPage((ulong)_modId);
        }
    }

    [RelayCommand]
    private void OpenLocalFolder()
    {
        if (IsWorkshopMod || _steam.AppInstallDir == string.Empty) return;

        string path = Path.GetFullPath(Path.Combine(_steam.AppInstallDir, "ConanSandbox/Mods", _localFolder));

        if (Directory.Exists(path))
            Process.Start("explorer.exe", path);
    }
}