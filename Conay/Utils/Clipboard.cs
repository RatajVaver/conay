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

    public static IClipboard? Get() => AppWindow.GetTopLevel()?.Clipboard;

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