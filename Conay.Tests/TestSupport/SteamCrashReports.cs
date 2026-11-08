using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Conay.Tests.TestSupport;

// Each of Steam's crashes left an application error in the Windows event log, whichever process it was
[SupportedOSPlatform("windows")]
internal static class SteamCrashReports
{
    public static void AssertNoneSince(DateTime utcTime) =>
        Assert.False(AnySince(utcTime), "Steam crashed, see the application error in the Windows event log");

    private static bool AnySince(DateTime utcTime)
    {
        ProcessStartInfo startInfo = new("wevtutil")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (string arg in new[]
                 {
                     "qe", "Application", "/f:xml",
                     "/q:*[System[Provider[@Name='Application Error'] and " +
                     $"TimeCreated[@SystemTime>='{utcTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}']]]"
                 })
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process query = Process.Start(startInfo)!;
        string events = query.StandardOutput.ReadToEnd();
        query.WaitForExit();

        return Regex.IsMatch(events, @"<Data[^>]*>steam\.exe</Data>", RegexOptions.IgnoreCase);
    }
}
