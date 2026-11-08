using System.Diagnostics;
using System.Runtime.Versioning;
using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using Conay.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conay.Tests.Services;

// Switches accounts with the real Conay.exe, handed over the way a switch started in Conay is: the running,
// connected Conay starts a new instance to finish the switch, and exits. Then watches for everything switching
// broke before: Steam crashing or not coming back, Conay freezing, and Conan Exiles stuck as running.
// Opt in with CONAY_REAL_STEAM=1. Needs Conay built in Debug, Steam running and logged in, a second account saved
// with "Remember me", and Conay and Conan Exiles closed. Conay's window opens a few times, with its Debug config.
[Collection(RealSteamCollection.Name)]
[SupportedOSPlatform("windows")]
public class RealConaySwitchTests : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SwitchTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan WatchTime = TimeSpan.FromSeconds(30);

    // How long the previous instance stays open after starting the new one, less than the 10s after which the new
    // one closes it itself
    private static readonly TimeSpan HandOverTime = TimeSpan.FromSeconds(5);

    private static string ConayExe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "Conay", "bin", "Debug", "net9.0", "Conay.exe"));

    // Steam logs each process it tracks as a running game here, and when it stops tracking it
    private static string GameProcessLog =>
        Path.Combine(SteamPaths.FindSteamRoot() ?? string.Empty, "logs", "gameprocess_log.txt");

    private readonly TempDirectory _dir = new();
    private readonly SteamHost _host = new(NullLogger<SteamHost>.Instance, new NotifyService());
    private readonly List<Process> _conays = [];
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private long _logStart;

    public void Dispose()
    {
        foreach (Process conay in _conays)
        {
            Close(conay);
            conay.Dispose();
        }

        _dir.Dispose();
    }

    [RealSteamFact]
    public async Task SwitchingThereAndBack_LeavesSteamAndConayWorking()
    {
        Assert.True(File.Exists(ConayExe), $"Build Conay first, {ConayExe} is missing");
        Assert.True(Process.GetProcessesByName("Conay").Length == 0, "Close Conay first");
        Assert.False(new RegistrySteamConnection().IsGameRunning, "Close Conan Exiles first");
        ulong original = RegistrySteamConnection.ActiveSteamId();
        Assert.True(Steam.IsSteamRunning() && original != 0, "Start Steam and log in before running this test");
        Assert.False(_host.IsGameMarkedRunning, "Steam already shows Conan Exiles as running, restart it first");

        SteamAccount? other = TestServices
            .CreateSteamAccounts(TestServices.CreateConfig(_dir), SteamPaths.FindSteamRoot())
            .GetSavedAccounts()
            .FirstOrDefault(a => a.SteamId != original && a.RememberPassword);
        Assert.True(other != null, "Save a second account in Steam with \"Remember me\" to run this test");

        _logStart = new FileInfo(GameProcessLog).Length;

        try
        {
            Process conay = StartConay();
            await WaitUntil(() => _host.IsGameMarkedRunning, ConnectTimeout, "Conay did not connect to Steam");

            conay = await SwitchFrom(conay, other.SteamId);
            conay = await SwitchFrom(conay, original);
            Close(conay);

            await WaitUntil(() => !_host.IsGameMarkedRunning, TimeSpan.FromSeconds(15),
                "Steam still shows Conan Exiles as running after Conay closed");
            AssertSteamDidNotCrash();
        }
        finally
        {
            await RestoreAccount(original);
        }
    }

    // Hands over like a switch started in Conay. The new instance waits for the previous one to exit and for Steam
    // to stop tracking it, then closes Steam, has it log into the account and connects to it.
    private async Task<Process> SwitchFrom(Process previous, ulong steamId)
    {
        int steamPid = SteamPid();
        List<string> command =
            AppRestart.BuildCommand(ConayExe, null, new SwitchRequest(steamId).ToArgs(), previous.Id);
        Process next = StartConay(command.Skip(1).ToArray());

        Stopwatch handingOver = Stopwatch.StartNew();
        while (handingOver.Elapsed < HandOverTime)
        {
            Assert.True(SteamPid() == steamPid, "The new Conay closed Steam while the previous one was still open");
            await Task.Delay(250);
        }

        Close(previous);

        await WaitUntil(() => SteamPid() is var pid && pid != 0 && pid != steamPid &&
                              RegistrySteamConnection.ActiveSteamId() == steamId && _host.IsGameMarkedRunning,
            SwitchTimeout, $"Steam did not come back logged into {steamId} with the new Conay connected to it");

        // The new instance waits for Steam to let go of the previous one before closing it
        Assert.True(GameProcessLogSinceStart().Contains($"no longer tracking PID {previous.Id},"),
            "Steam was closed while it still tracked the previous Conay as Conan Exiles");

        await Watch(next);
        return next;
    }

    // Steam crashed within seconds of switches before, and Conay froze
    private async Task Watch(Process conay)
    {
        int steamPid = SteamPid();

        Stopwatch watched = Stopwatch.StartNew();
        while (watched.Elapsed < WatchTime)
        {
            AssertSteamDidNotCrash();
            Assert.True(SteamPid() == steamPid, "Steam closed or restarted after the switch");
            Assert.False(conay.HasExited, "Conay closed after the switch");

            conay.Refresh();
            Assert.True(conay.Responding, "Conay stopped responding after the switch");

            await Task.Delay(1000);
        }
    }

    // Started the way the previous instance starts the new one, with the environment it started with itself
    private Process StartConay(params string[] args)
    {
        ProcessStartInfo startInfo = AppRestart.CreateStartInfo([ConayExe, .. args], StartupEnvironment.Variables);
        startInfo.WorkingDirectory = Path.GetDirectoryName(ConayExe);

        Process conay = Process.Start(startInfo)!;
        _conays.Add(conay);
        return conay;
    }

    private static void Close(Process conay)
    {
        if (conay.HasExited) return;

        conay.CloseMainWindow();
        if (conay.WaitForExit(15000)) return;

        conay.Kill();
        conay.WaitForExit();
    }

    // Puts Steam back on the account the test started with, if a failure left it elsewhere
    private async Task RestoreAccount(ulong original)
    {
        foreach (Process conay in _conays)
            Close(conay);

        if (Steam.IsSteamRunning() && RegistrySteamConnection.ActiveSteamId() == original) return;

        try
        {
            SteamAccounts accounts = new(NullLogger<SteamAccounts>.Instance, new RegistrySteamConnection(), _host,
                TestServices.CreateConfig(_dir), new NotifyService())
            {
                PendingSwitch = new SwitchRequest(original)
            };

            await accounts.FinishPendingSwitch();
        }
        catch
        {
            // ignored, the failure that got here matters more
        }
    }

    private void AssertSteamDidNotCrash() => SteamCrashReports.AssertNoneSince(_startedAt);

    private string GameProcessLogSinceStart()
    {
        using FileStream log = new(GameProcessLog, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        // Steam starts a new log once it gets too big
        if (log.Length >= _logStart)
            log.Seek(_logStart, SeekOrigin.Begin);

        using StreamReader reader = new(log);
        return reader.ReadToEnd();
    }

    private static int SteamPid()
    {
        Process[] processes = Process.GetProcessesByName("steam");
        int pid = processes.Length > 0 ? processes[0].Id : 0;

        foreach (Process process in processes)
            process.Dispose();

        return pid;
    }

    private async Task WaitUntil(Func<bool> condition, TimeSpan timeout, string failure)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (!condition())
        {
            AssertSteamDidNotCrash();
            Assert.True(waited.Elapsed < timeout, failure);
            await Task.Delay(1000);
        }
    }
}
