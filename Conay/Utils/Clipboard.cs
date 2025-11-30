using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Conay.Utils;

public static class Clipboard
{
    public static IClipboard? Get()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        TopLevel? topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        return topLevel?.Clipboard;
    }
}