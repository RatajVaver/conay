using System.Diagnostics;
using System.Reflection;

namespace Conay.Utils;

public static class Meta
{
    public static string GetVersion()
    {
        string version = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).ProductVersion!;
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