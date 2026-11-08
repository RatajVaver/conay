using System.Diagnostics;
using System.Runtime.Versioning;
using Conay.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace Conay.Tests.TestSupport;

// Conay's view of the real Steam without the Steam API: which account is logged in comes from Steam's registry
[SupportedOSPlatform("windows")]
internal sealed class RegistrySteamConnection : ISteamConnection
{
    private const ulong SteamIdBase = 76561197960265728;

    public bool DualInstallMode => false;

    public bool IsGameRunning
    {
        get
        {
            List<Process> processes = Steam.GetGameProcesses();
            foreach (Process process in processes)
                process.Dispose();

            return processes.Count > 0;
        }
    }

    public bool IsUpdatingMods => false;

    public ulong CurrentSteamId => Steam.IsSteamRunning() ? ActiveSteamId() : 0;

    public Task WaitForSteam() => Task.CompletedTask;

    public void Disconnect()
    {
    }

    public void Resume()
    {
    }

    public void LaunchSteam() => Steam.StartClient(NullLogger.Instance);

    // Steam's record of who is logged in, 0 if nobody is (or Steam never wrote it)
    public static ulong ActiveSteamId()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        return key?.GetValue("ActiveUser") is int accountId and not 0
            ? SteamIdBase + unchecked((uint)accountId)
            : 0;
    }
}

// The real host, except that handing over only records the arguments instead of restarting the test run, the
// test then plays the new Conay instance
internal sealed class LocalHandOver(ISteamHost host) : ISteamHost
{
    public string[]? HandedOverArgs { get; private set; }

    public bool IsSteamRunning => host.IsSteamRunning;

    public bool IsGameMarkedRunning => host.IsGameMarkedRunning;

    public Task<bool> ShutdownSteam() => host.ShutdownSteam();

    public void ClearRunningApps() => host.ClearRunningApps();

    public void SetAutoLogin(string accountName) => host.SetAutoLogin(accountName);

    public bool RestartConay(string[] args)
    {
        HandedOverArgs = args;
        return true;
    }
}
