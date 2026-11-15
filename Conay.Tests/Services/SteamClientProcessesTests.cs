using System.Diagnostics;
using System.Runtime.Versioning;
using Conay.Services;
using Conay.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conay.Tests.Services;

// Keeping track of Steam's processes while it starts, with a copy of cmd.exe standing in for steam.exe
[SupportedOSPlatform("windows")]
public class SteamClientProcessesTests : IDisposable
{
    private const string Name = "conay-test-steam";

    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    // Runs about two seconds, then exits with the given code
    private Process StartFakeSteam(int exitCode)
    {
        string exe = _dir.PathOf(Name + ".exe");
        if (!File.Exists(exe))
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), exe);

        return Process.Start(new ProcessStartInfo(exe, $"/c ping -n 3 127.0.0.1 >nul & exit {exitCode}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
    }

    [WindowsFact]
    public async Task ExitCode_CanBeReadAfterTheProcessIsGone()
    {
        using SteamClientProcesses processes = new(NullLogger.Instance) { ProcessName = Name };
        using Process fakeSteam = StartFakeSteam(7);

        SteamClientProcess running = Assert.Single(processes.Poll());
        Assert.Equal(fakeSteam.Id, running.Id);
        Assert.Null(running.ExitCode);

        await fakeSteam.WaitForExitAsync();

        SteamClientProcess exited = Assert.Single(processes.Poll());
        Assert.Equal(7, exited.ExitCode);
    }

    // Windows' copy of a crashed Steam is recognised by the process that started it
    [WindowsFact]
    public void Poll_KnowsWhoStartedEachProcess()
    {
        using SteamClientProcesses processes = new(NullLogger.Instance) { ProcessName = Name };
        using Process fakeSteam = StartFakeSteam(0);

        SteamClientProcess seen = Assert.Single(processes.Poll());

        Assert.Equal(Environment.ProcessId, seen.ParentId);
        Assert.NotNull(seen.StartTime);
        fakeSteam.Kill();
    }

    [WindowsFact]
    public void Start_StartsSteam()
    {
        int starts = 0;
        using SteamClientProcesses processes = new(NullLogger.Instance) { ProcessName = Name, StartClient = () => starts++ };

        processes.Start();

        Assert.Equal(1, starts);
    }
}
