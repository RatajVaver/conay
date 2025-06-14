using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Conay.Utils;

public static class Clipboard
{
    public static IClipboard Get()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
        {
            MainWindow: { } window
        }
            ? window.Clipboard!
            : null!;
    }
}