using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Conay.Utils;

public static class AppWindow
{
    public static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        return TopLevel.GetTopLevel(desktop.MainWindow);
    }
}