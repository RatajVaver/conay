using System.Diagnostics;
using System.Runtime.Versioning;
using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using Conay.Tests.Utils;
using Conay.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Steamworks;

namespace Conay.Tests.Services;

// Starts the real Steam to see what crashes it while it starts up, which switching accounts kept running into.
// Opt in with CONAY_REAL_STEAM=1, and run each test in its own test run: the Steam API leaves state behind in the
// process. Steam gets closed first, Conay and Conan Exiles have to be closed.
[Collection(RealSteamCollection.Name)]
[SupportedOSPlatform("windows")]
public class RealSteamStartupTests
{
    private static readonly TimeSpan WatchTime = TimeSpan.FromSeconds(45);

    [RealSteamFact]
    public async Task Steam_SurvivesStarting()
    {
        await CloseSteam();

        await StartSteamAndWatchIt();
    }

    [RealSteamFact]
    public async Task Steam_SurvivesStartingAfterAFailedSteamApiConnect()
    {
        await CloseSteam();
        Assert.ThrowsAny<Exception>(() => SteamClient.Init(GameVersionHelper.AppId));

        await StartSteamAndWatchIt();
    }

    // Like a Conay that connected to Steam as Conan Exiles and then started it, with the game's settings that put
    // into its environment
    [RealSteamFact]
    public async Task Steam_SurvivesBeingStartedByAConayThatConnectedToSteam()
    {
        StartupEnvironment.Capture();
        await CloseSteam();

        foreach ((string name, string value) in StartupEnvironmentTests.ConanExilesGameEnvironment)
            Environment.SetEnvironmentVariable(name, value);

        try
        {
            await StartSteamAndWatchIt();
        }
        finally
        {
            foreach (string name in StartupEnvironmentTests.ConanExilesGameEnvironment.Keys)
                Environment.SetEnvironmentVariable(name, null);
        }
    }

    private static async Task CloseSteam()
    {
        Assert.True(Process.GetProcessesByName("Conay").Length == 0, "Close Conay first, it talks to Steam on its own");
        Assert.True(await new SteamHost(NullLogger<SteamHost>.Instance, new NotifyService()).ShutdownSteam(),
            "Could not close Steam");
    }

    private static async Task StartSteamAndWatchIt()
    {
        DateTime startedAt = DateTime.UtcNow;
        Steam.StartClient(NullLogger.Instance);
        Process steam = await WaitForSteamProcess();

        Stopwatch watched = Stopwatch.StartNew();
        while (watched.Elapsed < WatchTime)
        {
            if (steam.HasExited)
            {
                Assert.True(steam.ExitCode == 0,
                    $"Steam crashed {watched.Elapsed.TotalSeconds:0}s into starting up (exit code 0x{steam.ExitCode:X8})");

                // Exited normally, so it restarted itself (e.g. to update)
                steam = await WaitForSteamProcess();
            }

            await Task.Delay(500);
        }

        SteamCrashReports.AssertNoneSince(startedAt);
    }

    private static async Task<Process> WaitForSteamProcess()
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (waited.Elapsed < TimeSpan.FromSeconds(30))
        {
            Process? steam = Process.GetProcessesByName("steam").FirstOrDefault();
            if (steam != null)
            {
                // Holding the handle keeps the exit code readable after Steam is gone
                _ = steam.Handle;
                return steam;
            }

            await Task.Delay(250);
        }

        Assert.Fail("Steam did not start");
        return null!;
    }
}
