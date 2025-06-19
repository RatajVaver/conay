using System.Diagnostics;
using System.Reflection;

namespace Conay.Utils;

public static class Meta
{
    public static string GetVersion()
    {
        string filePath = Process.GetCurrentProcess().MainModule?.FileName!;
        string version = FileVersionInfo.GetVersionInfo(filePath).ProductVersion!;
        return version;
    }

    public static string GetAssemblyVersion()
    {
        string version = Assembly.GetEntryAssembly()!.GetName().Version!.ToString();
        return version[..version.LastIndexOf('.')];
    }

    public static string GetUserAgent()
    {
        return "Conay v" + GetVersion();
    }
}