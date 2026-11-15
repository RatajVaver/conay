using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Conay.Utils;

public static class SteamPaths
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [SupportedOSPlatform("linux")]
    public static string LinuxRegistryPath => Path.Combine(Home, ".steam", "registry.vdf");

    public static string? FindSteamRoot()
    {
        if (OperatingSystem.IsWindows())
            return FindWindowsSteamRoot();

        return OperatingSystem.IsLinux() ? FindLinuxSteamRoot() : null;
    }

    [SupportedOSPlatform("windows")]
    private static string? FindWindowsSteamRoot()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string path && Directory.Exists(path))
                return Path.GetFullPath(path);
        }
        catch
        {
            // ignored
        }

        return null;
    }

    [SupportedOSPlatform("linux")]
    public static string? FindLinuxSteamRoot()
    {
        foreach (string dir in new[] { ".steam/root", ".local/share/Steam" }.Select(d => Path.Combine(Home, d)))
        {
            if (!Directory.Exists(Path.Combine(dir, "steamapps"))) continue;

            try
            {
                FileSystemInfo? target = Directory.ResolveLinkTarget(dir, returnFinalTarget: true);
                if (target is { Exists: true })
                    return target.FullName;
            }
            catch
            {
                // ignored
            }

            return dir;
        }

        return null;
    }
}
