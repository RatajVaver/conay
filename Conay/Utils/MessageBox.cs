using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System.Threading.Tasks;

namespace Conay.Utils;

public static class MessageBox
{
    public static Task<bool> Confirm(string message) => Show(message, true);

    public static void ShowInfo(string message) => _ = Show(message, false);

    private static async Task<bool> Show(string message, bool hasYesNo)
    {
        Window? owner = GetMainWindow();

        DockPanel titleBar = new()
        {
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#000")),
        };

        TextBlock titleText = new()
        {
            Text = "Conay",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Foreground = new SolidColorBrush(Color.Parse("#fcfcfc")),
            FontSize = 12,
        };

        Button closeBtn = new()
        {
            Content = "✕",
            Width = 46,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#fcfcfc")),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 11,
        };

        DockPanel.SetDock(closeBtn, Dock.Right);
        titleBar.Children.Add(closeBtn);

        DockPanel.SetDock(titleText, Dock.Left);
        titleBar.Children.Add(titleText);

        TextBlock msgText = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#fcfcfc")),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 20),
        };

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        Window dialog = new()
        {
            Title = "Conay",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation =
                owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.Parse("#111")),
            RequestedThemeVariant = ThemeVariant.Dark,
            WindowDecorations = WindowDecorations.BorderOnly,
            ExtendClientAreaToDecorationsHint = true,
            Content = new StackPanel
            {
                Children =
                {
                    titleBar,
                    new StackPanel { Margin = new Thickness(24), Children = { msgText, buttonRow } },
                }
            },
        };

        titleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(titleBar).Properties.IsLeftButtonPressed)
                dialog.BeginMoveDrag(e);
        };
        closeBtn.Click += (_, _) => dialog.Close(false);
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) dialog.Close(false);
        };

        if (hasYesNo)
        {
            Button yes = CreateButton("Yes");
            yes.Click += (_, _) => dialog.Close(true);
            Button no = CreateButton("No");
            no.Click += (_, _) => dialog.Close(false);
            buttonRow.Children.Add(yes);
            buttonRow.Children.Add(no);
        }
        else
        {
            Button ok = CreateButton("OK");
            ok.Click += (_, _) => dialog.Close(true);
            buttonRow.Children.Add(ok);
        }

        if (owner != null)
            return await dialog.ShowDialog<bool>(owner);

        dialog.Show();
        return false;
    }

    private static Button CreateButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(16, 8),
        MinWidth = 80,
        FontSize = 12,
        Background = new SolidColorBrush(Color.Parse("#222")),
        Foreground = new SolidColorBrush(Color.Parse("#fcfcfc")),
        BorderThickness = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}