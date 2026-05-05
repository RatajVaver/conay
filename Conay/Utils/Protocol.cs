using System;
using System.Diagnostics;

namespace Conay.Utils;

public static class Protocol
{
    public static void Open(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);

            if (!OperatingSystem.IsWindows() && link.StartsWith("steam://"))
            {
                OpenViaSteamExecutable(link);
            }
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private static void OpenViaSteamExecutable(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam",
                Arguments = uri,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open Steam URI: {ex.Message}");
        }
    }
}