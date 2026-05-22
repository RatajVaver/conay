using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Conay.Utils;

public static class Clipboard
{
    [SupportedOSPlatform("windows")]
    [DllImport("ole32.dll")]
    private static extern int OleFlushClipboard();

    public static IClipboard? Get()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        TopLevel? topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        return topLevel?.Clipboard;
    }

    public static async Task SetTextAsync(string text)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            IClipboard? clipboard = Get();
            if (clipboard is null) return;
            DataTransfer clipData = new();
            clipData.Add(DataTransferItem.CreateText(text));
            await clipboard.SetDataAsync(clipData);
            if (OperatingSystem.IsWindows())
                OleFlushClipboard();
        });
    }
}