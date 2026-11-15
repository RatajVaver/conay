using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Conay.ViewModels.Parts;

namespace Conay.Views.Parts;

public partial class ServerPresetView : UserControl
{
    public ServerPresetView()
    {
        InitializeComponent();
    }

    private void AccountFlyout_OnOpening(object? sender, EventArgs e)
    {
        (DataContext as ServerPresetViewModel)?.RefreshAccountOptions();
    }
}