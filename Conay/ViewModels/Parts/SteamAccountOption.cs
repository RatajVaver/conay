using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Conay.ViewModels.Parts;

// A choice in a server's Steam account menu; without isSelected it's a plain action instead of a radio choice
public partial class SteamAccountOption(string name, Action onSelect, bool? isSelected = null) : ViewModelBase
{
    public string Name { get; } = name;
    public bool IsSelected { get; } = isSelected ?? false;
    public MenuItemToggleType ToggleType { get; } = isSelected.HasValue ? MenuItemToggleType.Radio : MenuItemToggleType.None;

    [RelayCommand]
    private void Select() => onSelect();
}
