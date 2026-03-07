using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Services;

namespace Conay.ViewModels.Parts;

public partial class SaveItemViewModel(
    string slug,
    string name,
    string size,
    string lastPlayed,
    int modCount,
    bool isCurrentlyLoaded,
    SaveManager saveManager,
    Action<SaveItemViewModel> onLoad,
    Action<SaveItemViewModel> onDelete)
    : ViewModelBase
{
    public string Slug { get; } = slug;

    [ObservableProperty]
    private string _name = name;

    [ObservableProperty]
    private string _size = size;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string _lastPlayed = lastPlayed;

    public int ModCount { get; } = modCount;

    public string Details => $"{ModCount} mods  ·  {Size}  ·  {LastPlayed}";

    [ObservableProperty]
    private bool _isCurrentlyLoaded = isCurrentlyLoaded;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRenameCommand))]
    private string _editName = string.Empty;

    [RelayCommand]
    private void Load() => onLoad(this);

    [RelayCommand]
    private void StartRename()
    {
        EditName = Name;
        IsRenaming = true;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmRename))]
    private void ConfirmRename()
    {
        string newName = EditName.Trim();
        if (saveManager.RenameSave(Slug, newName))
            Name = newName;
        IsRenaming = false;
    }

    private bool CanConfirmRename() => !string.IsNullOrWhiteSpace(EditName);

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    [RelayCommand]
    private void Delete() => onDelete(this);
}