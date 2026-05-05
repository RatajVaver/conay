using System.Reflection;

namespace Conay.Utils;

public static class Meta
{
    public static string GetVersion()
    {
        return Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? string.Empty;
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