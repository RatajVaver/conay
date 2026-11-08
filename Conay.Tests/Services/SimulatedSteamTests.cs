using Conay.Tests.TestSupport;

namespace Conay.Tests.Services;

// The simulated Steam has to break the way the real one did, or the switching tests prove nothing. Each case here
// was seen on the real Steam while account switching was built (its gameprocess_log, registry and Conay's log).
public class SimulatedSteamTests : IDisposable
{
    private readonly AccountSwitchingSetup _setup = new();

    public void Dispose() => _setup.Dispose();

    [Fact]
    public void Disconnecting_DoesNotStopSteamTrackingConay()
    {
        (SimulatedConay conay, _) = _setup.StartConay();

        conay.Disconnect();

        Assert.Contains(conay, _setup.Steam.Tracked);
        Assert.True(_setup.Steam.ConanMarkedRunning);
    }

    [Fact]
    public async Task ConayExiting_StopsSteamTrackingIt_AMomentLater()
    {
        _setup.Steam.ExitNoticeDelay = TimeSpan.FromMilliseconds(200);
        (SimulatedConay conay, _) = _setup.StartConay();

        conay.Exit();
        bool trackedRightAfter = _setup.Steam.Tracked.Contains(conay) && _setup.Steam.IsGameMarkedRunning;
        await _setup.Steam.WaitForExitsToBeNoticed();

        Assert.True(trackedRightAfter);
        Assert.Empty(_setup.Steam.Tracked);
        Assert.False(_setup.Steam.IsGameMarkedRunning);
    }

    [Fact]
    public async Task ConayStillOpen_KeepsConanExilesMarkedRunning()
    {
        _setup.StartConay();
        (SimulatedConay exiting, _) = _setup.StartConay();

        exiting.Exit();
        await _setup.Steam.WaitForExitsToBeNoticed();

        Assert.True(_setup.Steam.IsGameMarkedRunning);
    }

    [Fact]
    public async Task KilledAfterItNoticedConayExited_StartsCleanly()
    {
        (SimulatedConay conay, _) = _setup.StartConay();
        conay.Exit();
        await _setup.Steam.WaitForExitsToBeNoticed();

        await _setup.Steam.ShutdownSteam();
        _setup.Steam.Start();

        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task KilledWhileTrackingNothing_StartsCleanly()
    {
        await _setup.Steam.ShutdownSteam();
        _setup.Steam.Start();

        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task KilledWhileConayIsConnected_FreezesConay()
    {
        _setup.StartConay();

        await _setup.Steam.ShutdownSteam();

        Assert.Contains(_setup.Steam.Problems, p => p.Contains("freezes Conay"));
    }

    [Fact]
    public async Task KilledWhileTrackingConay_KeepsConanMarkedRunning()
    {
        (SimulatedConay conay, _) = _setup.StartConay();
        conay.Disconnect();

        await _setup.Steam.ShutdownSteam();

        Assert.True(_setup.Steam.ConanMarkedRunning);
    }

    [Fact]
    public async Task StartedWithConanStillMarkedRunning_ShowsItRunningForever()
    {
        (SimulatedConay conay, _) = _setup.StartConay();
        conay.Disconnect();
        await _setup.Steam.ShutdownSteam();

        _setup.Steam.Start();

        Assert.Contains(_setup.Steam.Problems, p => p.Contains("running forever"));
    }

    [Fact]
    public async Task ClearingRunningGamesAfterAKill_LetsItStartCleanly()
    {
        (SimulatedConay conay, _) = _setup.StartConay();
        conay.Disconnect();
        await _setup.Steam.ShutdownSteam();

        _setup.Steam.ClearRunningApps();
        _setup.Steam.Start();

        Assert.False(_setup.Steam.ConanMarkedRunning);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task FailedConnectWhileSteamIsClosed_DoesNotHurtItsNextStart()
    {
        await _setup.Steam.ShutdownSteam();
        (SimulatedConay conay, _) = _setup.StartConay();

        _setup.Steam.SetAutoLogin("alice_main");
        _setup.Steam.Start();
        conay.Resume();

        Assert.True(conay.IsConnected);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task ReconnectingToARestartedSteamFromTheSameProcess_IsFlagged()
    {
        (SimulatedConay conay, _) = _setup.StartConay();
        conay.Disconnect();
        await _setup.Steam.ShutdownSteam();
        _setup.Steam.ClearRunningApps();
        _setup.Steam.SetAutoLogin("alice_main");
        _setup.Steam.Start();

        conay.Resume();

        Assert.Contains(_setup.Steam.Problems, p => p.Contains("never notices it closing"));
    }
}
