using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Conay.Views;

public partial class AddPresetView : UserControl
{
    public AddPresetView()
    {
        InitializeComponent();
        FileNameBox.AddHandler(TextInputEvent, FilterFileName, RoutingStrategies.Tunnel);
    }

    private static void FilterFileName(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null) return;
        string filtered = AlphaNumeric().Replace(e.Text, "");
        if (filtered != e.Text)
            e.Text = filtered;
    }

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex AlphaNumeric();
}