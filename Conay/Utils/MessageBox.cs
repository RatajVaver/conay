using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System.Collections.Generic;
using System.Threading.Tasks;
using Conay.Data;

namespace Conay.Utils;

public static class MessageBox
{
    public static Task<bool> Confirm(string message) => Show(message, true);

    public static void ShowInfo(string message) => _ = Show(message, false);

    public static async Task<SteamAccount?> ChooseSteamAccount(string message, IReadOnlyList<SteamAccount> accounts,
        ulong currentSteamId)
    {
        Window? owner = GetMainWindow();
        if (owner == null) return null;

        StackPanel options = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 20) };
        StackPanel buttonRow = CreateButtonRow();
        Window dialog = CreateDialog(message, options, buttonRow, null);

        foreach (SteamAccount account in accounts)
        {
            Button option = CreateAccountButton(account, account.SteamId == currentSteamId);
            option.Click += (_, _) => dialog.Close(account);
            options.Children.Add(option);
        }

        Button cancel = CreateButton("Cancel");
        cancel.Click += (_, _) => dialog.Close(null);
        buttonRow.Children.Add(cancel);

        return await dialog.ShowDialog<SteamAccount?>(owner);
    }

    private static async Task<bool> Show(string message, bool hasYesNo)
    {
        Window? owner = GetMainWindow();
        StackPanel buttonRow = CreateButtonRow();
        Window dialog = CreateDialog(message, null, buttonRow, false);

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

    private static Window CreateDialog(string message, Control? content, StackPanel buttonRow, object? cancelResult)
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

        StackPanel body = new() { Margin = new Thickness(24), Children = { msgText } };
        if (content != null)
            body.Children.Add(content);
        body.Children.Add(buttonRow);

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
            Content = new StackPanel { Children = { titleBar, body } },
        };

        titleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(titleBar).Properties.IsLeftButtonPressed)
                dialog.BeginMoveDrag(e);
        };
        closeBtn.Click += (_, _) => dialog.Close(cancelResult);
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) dialog.Close(cancelResult);
        };

        return dialog;
    }

    private static StackPanel CreateButtonRow() => new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right,
        Spacing = 8,
    };

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

    private static Button CreateAccountButton(SteamAccount account, bool isCurrent)
    {
        string details = account.AccountName;
        if (isCurrent)
            details += " · current";
        if (!account.RememberPassword)
            details += " · login not saved";

        Image avatar = new()
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(0, 0, 10, 0),
            Source = Avatar.Load(account.AvatarPath),
        };

        StackPanel names = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = account.DisplayName,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.Parse("#fcfcfc")),
                },
                new TextBlock
                {
                    Text = details,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#888")),
                },
            },
        };
        Grid.SetColumn(names, 1);

        return new Button
        {
            Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *"), Children = { avatar, names } },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6),
            Background = new SolidColorBrush(Color.Parse("#1a1a1a")),
            BorderThickness = new Thickness(0),
        };
    }

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
