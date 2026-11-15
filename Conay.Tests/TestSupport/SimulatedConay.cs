using System.Diagnostics;
using Conay.Services;

namespace Conay.Tests.TestSupport;

// One running Conay process and its Steam API connection to a SimulatedSteam
internal sealed class SimulatedConay : ISteamConnection
{
    private readonly SimulatedSteam _steam;
    private int _modChecksUntilDownloadsFinish;

    // Without connect, it stays away from Steam until resumed, like the Steam service with a switch to finish
    public SimulatedConay(SimulatedSteam steam, bool connect = true)
    {
        _steam = steam;
        _steam.Add(this);

        if (connect)
            Connect();
    }

    public bool IsAlive { get; private set; } = true;
    public bool IsConnected { get; private set; }

    // How long ago the process exited
    public TimeSpan ExitedFor => IsAlive ? TimeSpan.Zero : Stopwatch.GetElapsedTime(_exitedAt);

    private long _exitedAt;

    // The Steam session this process registered with as "Conan Exiles", 0 if it never connected
    public int RegisteredSession { get; private set; }

    public bool DualInstallMode { get; set; } = true;
    public bool IsGameRunning { get; set; }

    // How many more times Conay reports mod downloads in progress before they finish
    public int ModChecksUntilDownloadsFinish
    {
        set => _modChecksUntilDownloadsFinish = value;
    }

    public bool IsUpdatingMods
    {
        get
        {
            if (_modChecksUntilDownloadsFinish <= 0) return false;
            _modChecksUntilDownloadsFinish--;
            return true;
        }
    }

    public ulong CurrentSteamId => IsConnected ? _steam.LoggedInAs : 0;

    public Task WaitForSteam() => Task.CompletedTask;

    public void Disconnect()
    {
        _steam.Record("Conay disconnects");
        IsConnected = false;
    }

    public void Resume()
    {
        _steam.Record("Conay reconnects");
        Connect();
    }

    public void LaunchSteam() => _steam.Start();

    // The process exits. If the Steam it registered with is still running, it stops tracking it a moment later.
    public void Exit()
    {
        _steam.Record("Conay exits");
        _exitedAt = Stopwatch.GetTimestamp();
        IsConnected = false;
        IsAlive = false;
    }

    internal void LoseConnection() => IsConnected = false;

    // Like Steam API init, this only works once Steam is running with someone logged in
    private void Connect()
    {
        if (!_steam.IsRunning || _steam.LoggedInAs == 0) return;

        if (RegisteredSession != 0 && RegisteredSession != _steam.Session)
            _steam.RecordProblem("Conay reconnected to a restarted Steam from the same process, " +
                                 "Steam never notices it closing");

        IsConnected = true;
        RegisteredSession = _steam.Session;
        _steam.Track(this);
    }
}
