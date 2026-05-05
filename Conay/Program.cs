using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Conay.Utils;
using Steamworks;

namespace Conay;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsLinux())
            NativeLibrary.SetDllImportResolver(typeof(SteamClient).Assembly, SteamApiResolver);

        DumpHelper.Enable();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static IntPtr SteamApiResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "steam_api64" &&
            NativeLibrary.TryLoad("libsteam_api.so", assembly, searchPath, out IntPtr handle))
            return handle;
        return IntPtr.Zero;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}