using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Conay.Services;

// Everything switching accounts does to Steam and to Conay itself outside of this process
public interface ISteamHost
{
    bool IsSteamRunning { get; }

    // Whether Steam shows Conan Exiles as running. Conay counts as it too, and Steam only notices a couple of
    // seconds after the process exits.
    bool IsGameMarkedRunning { get; }

    // Closes Steam, returns whether it's gone
    Task<bool> ShutdownSteam();

    // Forgets the games Steam thought were running, which a killed Steam never does on its own
    void ClearRunningApps();

    // The account Steam logs into automatically on its next start, empty for none
    void SetAutoLogin(string accountName);

    // Hands over to a new Conay instance started with args, returns whether it took over
    bool RestartConay(string[] args);
}

public class SteamHost(ILogger<SteamHost> logger, NotifyService notifyService) : ISteamHost
{
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    internal string RegistryKeyPath { get; init; } = @"Software\Valve\Steam";

    public bool IsSteamRunning => Steam.IsSteamRunning();

    public bool IsGameMarkedRunning
    {
        get
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    return IsWindowsGameMarkedRunning();
                if (OperatingSystem.IsLinux())
                    return IsLinuxGameMarkedRunning();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read running games from Steam registry!");
            }

            return false;
        }
    }

    public async Task<bool> ShutdownSteam()
    {
        if (!Steam.IsSteamRunning()) return true;

        // Steam won't exit on its own while Conay is connected, it keeps waiting for "Conan Exiles" to quit
        notifyService.UpdateStatus(this, "Closing Steam..");
        foreach (Process process in Process.GetProcessesByName("steam")
                     .Concat(Process.GetProcessesByName("steamwebhelper")))
        {
            try
            {
                process.Kill();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to kill Steam process ({Name})!", process.ProcessName);
            }
            finally
            {
                process.Dispose();
            }
        }

        if (await WaitForSteamExit(KillTimeout)) return true;

        logger.LogWarning("Could not kill Steam, asking it to exit instead..");
        Protocol.Open("steam://exit");
        return await WaitForSteamExit(ShutdownTimeout);
    }

    private static async Task<bool> WaitForSteamExit(TimeSpan timeout)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            if (!Steam.IsSteamRunning()) return true;
            await Task.Delay(500);
        }

        return !Steam.IsSteamRunning();
    }

    public void ClearRunningApps()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                ClearWindowsRunningApps();
            else if (OperatingSystem.IsLinux())
                ClearLinuxRunningApps();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear running games from Steam registry!");
        }
    }

    public void SetAutoLogin(string accountName)
    {
        if (OperatingSystem.IsWindows())
            SetWindowsAutoLogin(accountName);
        else if (OperatingSystem.IsLinux())
            SetLinuxAutoLogin(accountName);
    }

    public bool RestartConay(string[] args)
    {
        // Only returns if the new instance could not be started, otherwise this process exits
        AppRestart.Restart(args);
        return false;
    }

    [SupportedOSPlatform("windows")]
    private void SetWindowsAutoLogin(string accountName)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
        ApplyAutoLogin(key, accountName);
    }

    [SupportedOSPlatform("windows")]
    internal static void ApplyAutoLogin(RegistryKey steamKey, string accountName)
    {
        steamKey.SetValue("AutoLoginUser", accountName, RegistryValueKind.String);
        steamKey.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
    }

    [SupportedOSPlatform("linux")]
    private void SetLinuxAutoLogin(string accountName)
    {
        string path = SteamPaths.LinuxRegistryPath;
        if (!File.Exists(path))
        {
            logger.LogWarning("Steam registry not found at {Path}, relying on loginusers.vdf only", path);
            return;
        }

        VdfNode vdf = VdfNode.Parse(File.ReadAllText(path));
        ApplyAutoLogin(vdf, accountName);

        File.WriteAllText(path, vdf.ToString());
    }

    internal static void ApplyAutoLogin(VdfNode registry, string accountName)
    {
        VdfNode steamKey = registry.GetOrAddNode("Registry").GetOrAddNode("HKCU").GetOrAddNode("Software")
            .GetOrAddNode("Valve").GetOrAddNode("Steam");

        steamKey.SetString("AutoLoginUser", accountName);
        steamKey.SetString("RememberPassword", "1");
    }

    [SupportedOSPlatform("windows")]
    private bool IsWindowsGameMarkedRunning()
    {
        using RegistryKey? steamKey = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return steamKey != null && IsGameMarkedRunningIn(steamKey);
    }

    [SupportedOSPlatform("windows")]
    internal static bool IsGameMarkedRunningIn(RegistryKey steamKey)
    {
        if (steamKey.GetValue("RunningAppID") is int appId && appId == GameVersionHelper.AppId)
            return true;

        using RegistryKey? app = steamKey.OpenSubKey($@"Apps\{GameVersionHelper.AppId}");
        return app?.GetValue("Running") is int and not 0;
    }

    [SupportedOSPlatform("linux")]
    private static bool IsLinuxGameMarkedRunning()
    {
        string path = SteamPaths.LinuxRegistryPath;
        return File.Exists(path) && IsGameMarkedRunningIn(VdfNode.Parse(File.ReadAllText(path)));
    }

    internal static bool IsGameMarkedRunningIn(VdfNode registry)
    {
        VdfNode? steamKey = FindSteamKey(registry);
        if (steamKey == null) return false;

        string appId = GameVersionHelper.AppId.ToString();
        return steamKey.GetString("RunningAppID") == appId ||
               steamKey.GetNode("Apps")?.GetNode(appId)?.GetString("Running") is { } running && running != "0";
    }

    [SupportedOSPlatform("windows")]
    private void ClearWindowsRunningApps()
    {
        using RegistryKey? steamKey = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        if (steamKey != null)
            ClearRunningApps(steamKey);
    }

    [SupportedOSPlatform("windows")]
    internal static void ClearRunningApps(RegistryKey steamKey)
    {
        if (steamKey.GetValue("RunningAppID") is int and not 0)
            steamKey.SetValue("RunningAppID", 0, RegistryValueKind.DWord);

        using RegistryKey? apps = steamKey.OpenSubKey("Apps");
        if (apps == null) return;

        foreach (string appId in apps.GetSubKeyNames())
        {
            using RegistryKey? app = apps.OpenSubKey(appId, writable: true);
            if (app?.GetValue("Running") is int and not 0)
                app.SetValue("Running", 0, RegistryValueKind.DWord);
        }
    }

    [SupportedOSPlatform("linux")]
    private static void ClearLinuxRunningApps()
    {
        string path = SteamPaths.LinuxRegistryPath;
        if (!File.Exists(path)) return;

        VdfNode vdf = VdfNode.Parse(File.ReadAllText(path));
        if (ClearRunningApps(vdf))
            File.WriteAllText(path, vdf.ToString());
    }

    // Returns whether anything was changed
    internal static bool ClearRunningApps(VdfNode registry)
    {
        VdfNode? steamKey = FindSteamKey(registry);
        if (steamKey == null) return false;

        bool changed = false;
        if (steamKey.GetString("RunningAppID") is { } runningAppId && runningAppId != "0")
        {
            steamKey.SetString("RunningAppID", "0");
            changed = true;
        }

        foreach ((string _, VdfNode app) in steamKey.GetNode("Apps")?.Children ?? [])
        {
            if (app.GetString("Running") is not { } running || running == "0") continue;

            app.SetString("Running", "0");
            changed = true;
        }

        return changed;
    }

    private static VdfNode? FindSteamKey(VdfNode registry) =>
        registry.GetNode("Registry")?.GetNode("HKCU")?.GetNode("Software")?.GetNode("Valve")?.GetNode("Steam");
}
