using Conay.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conay.Tests.Services;

// Watching a Steam that Conay started, which can crash a few seconds in (AMD's hook in it), with what was seen of
// the real one: crashes 1-12s in, Windows copying the crashing Steam to write the crash dump from (a process started
// by the crashed one, often one that can't be opened, left running for a while), Steam exiting with 42 as it starts
// its new version after updating itself, and a Steam started while another one runs (even one that's crashing)
// handing over to it and exiting right away
public class SteamStartupWatcherTests
{
    private const int AccessViolation = unchecked((int)0xC0000005);
    private const int SteamUpdated = 42;
    private const int ConayId = 1;

    private readonly Clock _clock = new();
    private readonly List<string> _statuses = [];

    // What happens to one Steam process
    private sealed record Fate(TimeSpan? ExitAfter, int ExitCode, bool Relaunches, bool Appears = true,
        bool CanBeOpened = true, TimeSpan DumpCopyFor = default, bool DumpCopyCanBeOpened = false)
    {
        public static readonly Fate KeepsRunning = new(null, 0, false);
        public static readonly Fate NeverAppears = new(null, 0, false, Appears: false);
        public static readonly Fate CantBeOpened = new(null, 0, false, CanBeOpened: false);

        public static Fate Crashes(double seconds, double dumpCopySeconds = 0, bool dumpCopyCanBeOpened = false) =>
            new(TimeSpan.FromSeconds(seconds), AccessViolation, false, DumpCopyFor: TimeSpan.FromSeconds(dumpCopySeconds),
                DumpCopyCanBeOpened: dumpCopyCanBeOpened);

        // Starts its new version (which gets the next fate) as it exits
        public static Fate UpdatesItself(double seconds) => new(TimeSpan.FromSeconds(seconds), SteamUpdated, true);

        public static Fate IsClosed(double seconds) => new(TimeSpan.FromSeconds(seconds), 0, false);

        // Ended by another program, like Task Manager
        public static Fate IsKilled(double seconds, int exitCode) =>
            new(TimeSpan.FromSeconds(seconds), exitCode, false);
    }

    private sealed class Clock
    {
        public DateTime Now { get; set; } = new(2026, 10, 16, 16, 0, 0);
    }

    // A Steam whose processes do what they're scripted to, one fate per Steam process in the order they start. Like
    // the real processes, the ones from before a restart stay around (and Windows' copies keep running), and a Steam
    // started while another one runs hands over to it and exits before it's seen.
    private sealed class FakeSteam(Clock clock, params Fate[] fates) : ISteamClientProcesses
    {
        private readonly Queue<Fate> _fates = new(fates);
        private readonly List<FakeProcess> _processes = [];
        private readonly HashSet<int> _seen = [];
        private int _nextId = 1000;

        private sealed record FakeProcess(int Id, int ParentId, DateTime Start, DateTime? ExitAt, int ExitCode,
            bool CanBeOpened, bool IsDumpCopy = false)
        {
            public bool IsRunning(DateTime now) => Start <= now && !(ExitAt <= now);
        }

        public List<DateTime> Restarts { get; } = [];

        // Conay starts Steam before it starts watching
        public FakeSteam StartedByConay()
        {
            Begin();
            return this;
        }

        // Ones that can be opened are known from when they're first seen running, with their exit code once they're
        // gone. The others are only listed while they run.
        public IReadOnlyList<SteamClientProcess> Poll()
        {
            DateTime now = clock.Now;
            foreach (FakeProcess process in _processes.Where(p => p.CanBeOpened && p.IsRunning(now)))
                _seen.Add(process.Id);

            return _processes.Where(p => p.CanBeOpened ? _seen.Contains(p.Id) : p.IsRunning(now))
                .Select(p => p.CanBeOpened
                    ? new SteamClientProcess(p.Id, p.ParentId, p.Start, p.ExitAt <= now ? p.ExitCode : null)
                    : new SteamClientProcess(p.Id, p.ParentId, null, null))
                .ToList();
        }

        public void Start()
        {
            Restarts.Add(clock.Now);
            Begin();
        }

        private void Begin()
        {
            if (_processes.Any(p => !p.IsDumpCopy && p.IsRunning(clock.Now))) return;

            Add(clock.Now, ConayId);
        }

        private void Add(DateTime start, int parentId)
        {
            Fate fate = _fates.Count > 0 ? _fates.Dequeue() : Fate.KeepsRunning;
            if (!fate.Appears) return;

            int id = _nextId++;
            DateTime? exitAt = start + fate.ExitAfter;
            _processes.Add(new FakeProcess(id, parentId, start, exitAt, fate.ExitCode, fate.CanBeOpened));

            // Windows copies the crashing Steam, then lets the crashed one exit a couple of seconds later
            if (exitAt is { } crashedAt && fate.DumpCopyFor > TimeSpan.Zero)
            {
                DateTime copiedAt = crashedAt - TimeSpan.FromSeconds(2) < start
                    ? start
                    : crashedAt - TimeSpan.FromSeconds(2);
                _processes.Add(new FakeProcess(_nextId++, id, copiedAt, copiedAt + fate.DumpCopyFor, 1,
                    fate.DumpCopyCanBeOpened, IsDumpCopy: true));
            }

            if (exitAt is { } exitedAt && fate.Relaunches)
                Add(exitedAt, id);
        }
    }

    private SteamStartupWatcher CreateWatcher(ISteamClientProcesses steam)
    {
        NotifyService notifyService = new();
        notifyService.StatusChanged += (_, status) => _statuses.Add(status);
        DateTime started = _clock.Now;

        return new SteamStartupWatcher(NullLogger.Instance, notifyService, steam)
        {
            Now = () => _clock.Now,
            Delay = delay =>
            {
                _clock.Now += delay;
                Assert.True(_clock.Now - started < TimeSpan.FromHours(1), "The watcher never finished");
                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task SteamThatKeepsRunning_IsLeftAlone()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Empty(steam.Restarts);
        Assert.Empty(_statuses);
    }

    [Fact]
    public async Task SteamThatCrashesWhileStarting_IsStartedAgain()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.Crashes(7), Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Single(steam.Restarts);
        Assert.Contains("Steam crashed while starting, starting it again..", _statuses);
    }

    // Seen for real: the copy couldn't be opened and Conay took it for a Steam it can't watch, so it stopped watching
    [Fact]
    public async Task WindowsCopyOfACrashingSteam_IsNotTakenForSteam()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.Crashes(7, dumpCopySeconds: 60), Fate.KeepsRunning)
            .StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Single(steam.Restarts);
    }

    [Fact]
    public async Task WindowsCopyOfACrashingSteam_ThatCanBeOpened_IsNotTakenForSteam()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.Crashes(7, dumpCopySeconds: 60, dumpCopyCanBeOpened: true),
            Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Single(steam.Restarts);
    }

    // Started while the crashed Steam is still there, the new one would only hand over to it
    [Fact]
    public async Task SteamThatCrashed_IsStartedAgainOnceItsGone_NotOnceWindowsCopyIs()
    {
        DateTime started = _clock.Now;
        FakeSteam steam = new FakeSteam(_clock, Fate.Crashes(7, dumpCopySeconds: 60), Fate.KeepsRunning)
            .StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        TimeSpan restartedAfter = Assert.Single(steam.Restarts) - started;
        Assert.InRange(restartedAfter, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(9));
    }

    [Fact]
    public async Task SteamThatCrashesAgain_WhileWindowsStillHasACopyOfTheFirst_IsStartedAgain()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.Crashes(7, dumpCopySeconds: 120), Fate.Crashes(6, dumpCopySeconds: 120),
            Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Equal(2, steam.Restarts.Count);
    }

    [Fact]
    public async Task SteamUpdatingItself_IsNotStartedAgain()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.UpdatesItself(8), Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Empty(steam.Restarts);
    }

    // Seen for real: Steam updated, started again and crashed 7s later
    [Fact]
    public async Task SteamThatCrashesAfterUpdatingItself_IsStartedAgain()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.UpdatesItself(8), Fate.Crashes(7, dumpCopySeconds: 30),
            Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Single(steam.Restarts);
    }

    // Seen for real: Steam updated itself 10s in and its new version crashed a second later. Started again while that
    // one was still crashing, the new Steam only handed over to it.
    [Fact]
    public async Task SteamThatCrashesRightAfterUpdatingItself_IsStartedAgainOnceItsGone()
    {
        DateTime started = _clock.Now;
        FakeSteam steam = new FakeSteam(_clock, Fate.UpdatesItself(10), Fate.Crashes(2, dumpCopySeconds: 60),
            Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        TimeSpan restartedAfter = Assert.Single(steam.Restarts) - started;
        Assert.InRange(restartedAfter, TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(14));
    }

    [Fact]
    public async Task SteamClosedWhileStarting_IsLeftClosed()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.IsClosed(10)).StartedByConay();

        Assert.False(await CreateWatcher(steam).Watch());

        Assert.Empty(steam.Restarts);
        Assert.Empty(_statuses);
    }

    // Ended in Task Manager (1) or by a program like Conay itself (-1)
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task SteamKilledWhileStarting_IsLeftClosed(int exitCode)
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.IsKilled(5, exitCode)).StartedByConay();

        Assert.False(await CreateWatcher(steam).Watch());

        Assert.Empty(steam.Restarts);
    }

    [Fact]
    public async Task SteamThatKeepsCrashing_IsGivenUpOn()
    {
        FakeSteam steam = new FakeSteam(_clock, Enumerable.Repeat(Fate.Crashes(5, dumpCopySeconds: 20), 20).ToArray())
            .StartedByConay();
        SteamStartupWatcher watcher = CreateWatcher(steam);

        Assert.False(await watcher.Watch());

        Assert.Equal(watcher.MaxRestarts, steam.Restarts.Count);
        Assert.Equal("Steam keeps crashing while starting! Start it manually.", _statuses[^1]);
    }

    [Fact]
    public async Task SteamThatNeverAppears_IsStartedAgain()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.NeverAppears, Fate.KeepsRunning).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Single(steam.Restarts);
        Assert.Contains("Steam did not start, starting it again..", _statuses);
    }

    // e.g. Steam runs as administrator, its processes can't be watched
    [Fact]
    public async Task SteamThatCantBeOpened_IsLeftAlone()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.CantBeOpened).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Empty(steam.Restarts);
    }

    [Fact]
    public async Task SteamThatUpdatedItselfIntoOneThatCantBeOpened_IsLeftAlone()
    {
        FakeSteam steam = new FakeSteam(_clock, Fate.UpdatesItself(8), Fate.CantBeOpened).StartedByConay();

        Assert.True(await CreateWatcher(steam).Watch());

        Assert.Empty(steam.Restarts);
    }
}
