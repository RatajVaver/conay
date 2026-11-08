using Conay.Services;

namespace Conay.Tests.Services;

// When Conay tries to connect to Steam: not while it's closed, and not while it's still starting up
public class SteamClientStateTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0);

    [Fact]
    public void NoSteamProcess_IsNotRunning()
    {
        Assert.Equal(Steam.ClientState.NotRunning, Steam.GetClientState([], Now));
    }

    [Fact]
    public void SteamStartedSecondsAgo_IsStarting()
    {
        Assert.Equal(Steam.ClientState.Starting, Steam.GetClientState([Now.AddSeconds(-5)], Now));
    }

    [Fact]
    public void SteamStartedAMinuteAgo_IsRunning()
    {
        Assert.Equal(Steam.ClientState.Running, Steam.GetClientState([Now.AddMinutes(-1)], Now));
    }

    [Fact]
    public void AnySteamProcessStillStarting_IsStarting()
    {
        Assert.Equal(Steam.ClientState.Starting,
            Steam.GetClientState([Now.AddMinutes(-1), Now.AddSeconds(-2)], Now));
    }

    // e.g. Steam runs as administrator, Conay can still try to connect to it
    [Fact]
    public void SteamProcessThatCantBeInspected_IsRunning()
    {
        Assert.Equal(Steam.ClientState.Running, Steam.GetClientState([null], Now));
    }
}
