using Conay.Services;
using Conay.Utils;

namespace Conay.Tests.TestSupport;

// Stands in for the Steam client on this machine, shared by every simulated Conay instance. It behaves the way
// the real one was seen to while account switching was built, and records a problem whenever it's handled in a
// way that broke the real one (frozen Conay, Conan Exiles stuck as running).
internal sealed class SimulatedSteam : ISteamHost
{
    private readonly string _loginUsersPath;
    private readonly List<string> _events = [];
    private readonly List<string> _problems = [];
    private readonly List<SimulatedConay> _instances = [];
    private readonly List<SimulatedConay> _tracked = [];

    public SimulatedSteam(string steamRoot, ulong loggedInAs, bool running = true)
    {
        _loginUsersPath = Path.Combine(steamRoot, "config", "loginusers.vdf");
        IsRunning = running;
        Session = running ? 1 : 0;
        LoggedInAs = running ? loggedInAs : 0;
    }

    public IReadOnlyList<string> Events => _events;
    public IReadOnlyList<string> Problems => _problems;

    public bool IsRunning { get; private set; }
    public int Session { get; private set; }
    public ulong LoggedInAs { get; private set; }
    public string? AutoLoginUser { get; private set; }

    // Steam's RunningAppID, set while it believes Conan Exiles (or Conay, which counts as it) is running
    public bool ConanMarkedRunning
    {
        get
        {
            NoticeExits();
            return _conanMarkedRunning;
        }
    }

    private bool _conanMarkedRunning;

    // Conay processes Steam currently tracks as a running Conan Exiles
    public IReadOnlyList<SimulatedConay> Tracked
    {
        get
        {
            NoticeExits();
            return _tracked;
        }
    }

    // The real Steam took 2.2-2.5s to stop tracking a Conay after it exited, it checks on its processes now and then
    public TimeSpan ExitNoticeDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    // Accounts whose saved login has expired, so Steam asks for a password instead of logging in
    public HashSet<string> ExpiredLogins { get; } = [];

    public bool CanShutdown { get; set; } = true;
    public bool HandOverSucceeds { get; set; } = true;
    public TaskCompletionSource? HoldShutdown { get; set; }
    public Action? OnHandOver { get; set; }
    public string[]? HandedOverArgs { get; private set; }

    public bool IsSteamRunning => IsRunning;

    // Runs whenever Conay checks whether Steam shows Conan Exiles as running
    public Action? OnRunningGameCheck { get; set; }

    public bool IsGameMarkedRunning
    {
        get
        {
            OnRunningGameCheck?.Invoke();
            return ConanMarkedRunning;
        }
    }

    // Waits long enough for Steam to have noticed every Conay that exited so far
    public Task WaitForExitsToBeNoticed() => Task.Delay(ExitNoticeDelay + TimeSpan.FromMilliseconds(30));

    public async Task<bool> ShutdownSteam()
    {
        _events.Add("kill Steam");
        if (HoldShutdown != null)
            await HoldShutdown.Task;

        if (!CanShutdown) return false;
        if (!IsRunning) return true;

        NoticeExits();

        // Seen for real: Steam API calls still in progress when Steam died never returned and froze Conay
        if (_instances.Any(i => i.IsConnected))
            _problems.Add("Steam was killed while Conay was connected to it, which freezes Conay");

        foreach (SimulatedConay instance in _instances)
            instance.LoseConnection();

        // Killed, it never gets to unmark what it tracked as running
        _tracked.Clear();
        IsRunning = false;
        LoggedInAs = 0;
        return true;
    }

    public void ClearRunningApps()
    {
        _events.Add("clear running games");
        if (IsRunning)
            _problems.Add("Running games were cleared while Steam was running");

        _conanMarkedRunning = false;
    }

    public void SetAutoLogin(string accountName)
    {
        _events.Add($"auto-login '{accountName}'");
        if (IsRunning)
            _problems.Add("Auto-login was changed while Steam was running, Steam overwrites it");

        AutoLoginUser = accountName;
    }

    public bool RestartConay(string[] args)
    {
        _events.Add("hand over: " + string.Join(' ', args));
        HandedOverArgs = args;
        OnHandOver?.Invoke();
        return HandOverSucceeds;
    }

    // Starts Steam, which logs into the auto-login account if its login is saved or shows the sign-in screen
    public void Start()
    {
        _events.Add("start Steam");
        if (IsRunning) return;

        if (_conanMarkedRunning)
            _problems.Add("Steam was started with Conan Exiles still marked as running, it shows as running forever");

        IsRunning = true;
        Session++;
        LoggedInAs = FindAutoLoginAccount();
    }

    // Someone signs in on Steam's sign-in screen
    public void SignIn(ulong steamId)
    {
        _events.Add($"sign in {steamId}");
        LoggedInAs = steamId;
    }

    internal void Add(SimulatedConay instance) => _instances.Add(instance);

    internal void Record(string evt) => _events.Add(evt);

    internal void RecordProblem(string problem) => _problems.Add(problem);

    // Steam API init registers the process as the game, until the process exits (disconnecting doesn't end it)
    internal void Track(SimulatedConay conay)
    {
        if (!_tracked.Contains(conay))
            _tracked.Add(conay);

        _conanMarkedRunning = true;
    }

    // Catches up on Steam checking its tracked processes: it stops tracking the ones that exited long enough ago
    private void NoticeExits()
    {
        if (!IsRunning) return;

        List<SimulatedConay> exited = _tracked.Where(c => !c.IsAlive && c.ExitedFor >= ExitNoticeDelay).ToList();
        if (exited.Count == 0) return;

        foreach (SimulatedConay conay in exited)
        {
            _events.Add("Steam stops tracking Conay");
            _tracked.Remove(conay);
        }

        if (_tracked.Count == 0)
            _conanMarkedRunning = false;
    }

    private ulong FindAutoLoginAccount()
    {
        if (string.IsNullOrEmpty(AutoLoginUser) || ExpiredLogins.Contains(AutoLoginUser) ||
            !File.Exists(_loginUsersPath))
            return 0;

        VdfNode? users = VdfNode.Parse(File.ReadAllText(_loginUsersPath)).GetNode("users");
        foreach ((string key, VdfNode user) in users?.Children ?? [])
        {
            if (user.GetString("AccountName") == AutoLoginUser && user.GetString("RememberPassword") == "1")
                return ulong.Parse(key);
        }

        return 0;
    }
}
