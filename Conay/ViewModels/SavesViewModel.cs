using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Services;
using Conay.Utils;
using Conay.ViewModels.Parts;
using Conay.Views;

namespace Conay.ViewModels;

public partial class SavesViewModel : PageViewModel
{
    private readonly SaveManager _saveManager;
    private readonly ModList _modList;
    private readonly Router _router;

    [ObservableProperty]
    private string _currentSaveName = "No save currently loaded";

    [ObservableProperty]
    private string _currentSaveDetails = string.Empty;

    [ObservableProperty]
    private bool _hasCurrentSave;

    [ObservableProperty]
    private bool _currentSaveIsKnown;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewSaveCommand))]
    private bool _showActionPanel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewSaveCommand))]
    private bool _showNamePanel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmSaveCurrentCommand))]
    private string _newSaveName = string.Empty;

    [ObservableProperty]
    private string _namePanelTitle = "Name your current save:";

    private string? _pendingLoadSlug;
    private bool _namingNewSave;

    public ObservableCollection<SaveItemViewModel> Saves { get; } = [];

    public SavesViewModel(SaveManager saveManager, ModList modList, Router router)
    {
        _saveManager = saveManager;
        _modList = modList;
        _router = router;

        Refresh();
        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    private void Refresh()
    {
        Saves.Clear();
        ShowActionPanel = false;
        ShowNamePanel = false;

        string? currentSlug = _saveManager.GetCurrentSaveSlug();
        List<FileInfo> activeFiles = _saveManager.GetActiveDbFiles();

        HasCurrentSave = activeFiles.Count > 0;

        if (!HasCurrentSave)
        {
            CurrentSaveName = "No save currently loaded";
            CurrentSaveDetails = string.Empty;
            CurrentSaveIsKnown = false;
        }
        else if (currentSlug != null)
        {
            SaveData? data = _saveManager.GetSaveData(currentSlug);
            if (data != null)
            {
                CurrentSaveName = data.Name;
                long size = activeFiles.Sum(f => f.Length);
                string when = data.LastPlayedAt.HasValue
                    ? HumanReadable.TimeAgo(data.LastPlayedAt.Value)
                    : "never played";
                CurrentSaveDetails = $"{FormatSize(size)}  ·  {when}";
                CurrentSaveIsKnown = true;
            }
            else
            {
                CurrentSaveName = "Unknown save";
                CurrentSaveDetails = FormatFileDetails(activeFiles);
                CurrentSaveIsKnown = false;
            }
        }
        else
        {
            CurrentSaveName = "Unsaved";
            CurrentSaveDetails = FormatFileDetails(activeFiles);
            CurrentSaveIsKnown = false;
        }

        foreach ((string slug, SaveData data, long size) in _saveManager.ListSaves())
        {
            bool isCurrent = slug == currentSlug && HasCurrentSave;
            string lastPlayed = data.LastPlayedAt.HasValue
                ? HumanReadable.TimeAgo(data.LastPlayedAt.Value)
                : "Never played";

            Saves.Add(new SaveItemViewModel(
                slug, data.Name, FormatSize(size), lastPlayed, data.Modlist.Count, isCurrent,
                _saveManager, OnLoadRequested, item => _ = OnDeleteRequestedAsync(item)));
        }
    }

    private void OnLoadRequested(SaveItemViewModel item)
    {
        if (Process.GetProcessesByName("ConanSandbox").Length > 0 || Process.GetProcessesByName("ConanSandbox_BE").Length > 0)
        {
            MessageBox.ShowInfo("Cannot swap saves while Conan Exiles is running. Close the game first.");
            return;
        }

        string? currentSlug = _saveManager.GetCurrentSaveSlug();

        if (currentSlug == item.Slug && HasCurrentSave)
        {
            _saveManager.UpdateLastPlayed(item.Slug);
            ApplyModlist(item.Slug);
            SaveData? same = _saveManager.GetSaveData(item.Slug);
            _router.ReadyForLaunch(new ServerData { Name = same?.Name ?? item.Slug, Ip = string.Empty },
                isSaveLaunch: true);
            return;
        }

        if (!HasCurrentSave)
        {
            ExecuteLoad(item.Slug, launch: true);
        }
        else if (CurrentSaveIsKnown)
        {
            _saveManager.UpdateSave(currentSlug!, _modList.GetCurrentModList());
            ExecuteLoad(item.Slug, launch: true);
        }
        else
        {
            _pendingLoadSlug = item.Slug;
            ShowActionPanel = true;
        }
    }

    [RelayCommand]
    private static void OpenSavesFolder()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "saves"));
        Directory.CreateDirectory(path);
        Process.Start("explorer.exe", path);
    }

    [RelayCommand(CanExecute = nameof(CanNewSave))]
    private void NewSave()
    {
        _namingNewSave = true;
        NamePanelTitle = "Name your new save:";
        ShowNamePanel = true;
    }

    [RelayCommand]
    private void SaveCurrent()
    {
        if (CurrentSaveIsKnown)
        {
            _saveManager.UpdateSave(_saveManager.GetCurrentSaveSlug()!, _modList.GetCurrentModList());
            Refresh();
            return;
        }

        _namingNewSave = false;
        NamePanelTitle = "Name your current save:";
        ShowNamePanel = true;
    }

    [RelayCommand]
    private void SaveCurrentAndLoad()
    {
        ShowActionPanel = false;
        _namingNewSave = false;
        NamePanelTitle = "Name your current save:";
        ShowNamePanel = true;
    }

    [RelayCommand]
    private async Task DiscardCurrentAndLoad()
    {
        ShowActionPanel = false;

        if (!await MessageBox.Confirm("Are you sure you want to discard the current save? This cannot be undone."))
        {
            _pendingLoadSlug = null;
            return;
        }

        _saveManager.DiscardCurrent();

        if (_pendingLoadSlug != null)
        {
            string slug = _pendingLoadSlug;
            _pendingLoadSlug = null;
            ExecuteLoad(slug, launch: true);
        }
        else
        {
            Refresh();
        }
    }

    [RelayCommand]
    private void CancelAction()
    {
        ShowActionPanel = false;
        ShowNamePanel = false;
        NewSaveName = string.Empty;
        NamePanelTitle = "Name your current save:";
        _pendingLoadSlug = null;
        _namingNewSave = false;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmSaveCurrent))]
    private void ConfirmSaveCurrent()
    {
        string name = NewSaveName.Trim();
        ShowNamePanel = false;
        NewSaveName = string.Empty;

        if (_namingNewSave)
        {
            _namingNewSave = false;
            NamePanelTitle = "Name your current save:";
            _saveManager.CreateNewSave(name, _modList.GetCurrentModList());
            Refresh();
            return;
        }

        _saveManager.SaveCurrent(name, _modList.GetCurrentModList());

        if (_pendingLoadSlug != null)
        {
            string slug = _pendingLoadSlug;
            _pendingLoadSlug = null;
            ExecuteLoad(slug, launch: true);
        }
        else
        {
            Refresh();
        }
    }

    private bool CanConfirmSaveCurrent() => !string.IsNullOrWhiteSpace(NewSaveName);
    private bool CanNewSave() => !ShowActionPanel && !ShowNamePanel;

    private void ExecuteLoad(string slug, bool launch = false)
    {
        _saveManager.LoadSave(slug);
        ApplyModlist(slug);

        if (launch)
        {
            SaveData? data = _saveManager.GetSaveData(slug);
            _router.ReadyForLaunch(new ServerData { Name = data?.Name ?? slug, Ip = string.Empty }, isSaveLaunch: true);
            return;
        }

        Refresh();
    }

    private void ApplyModlist(string slug)
    {
        SaveData? data = _saveManager.GetSaveData(slug);
        if (data == null) return;
        _modList.SaveModList([..data.Modlist]);
    }

    private async Task OnDeleteRequestedAsync(SaveItemViewModel item)
    {
        if (!await MessageBox.Confirm($"Are you sure you want to delete \"{item.Name}\"? This cannot be undone."))
            return;

        _saveManager.DeleteSave(item.Slug);
        Refresh();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
        return $"{bytes / (1024 * 1024.0):F1} MB";
    }

    private static string FormatFileDetails(List<FileInfo> files)
    {
        long size = files.Sum(f => f.Length);
        System.DateTime newest = files.Max(f => f.LastWriteTimeUtc);
        return $"{FormatSize(size)}  ·  {HumanReadable.TimeAgo(newest)}";
    }
}