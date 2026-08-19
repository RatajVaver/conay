using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Conay.ViewModels;

namespace Conay.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void Root_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
    }

    private void UnlistedServersTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SaveUnlistedServers();
    }

    private void UnlistedServersTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SettingsViewModel vm)
            vm.SaveUnlistedServers();

        UnlistedServersPanel.Focus();
        e.Handled = true;
    }
}
