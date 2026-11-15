using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using static Conay.Tests.TestSupport.AccountSwitchingSetup;

namespace Conay.Tests.Services;

// Switching accounts end to end against a simulated Steam that breaks the way the real one did: the Conay that
// starts the switch and hands over, and the fresh Conay that closes Steam, prepares its login, starts it again and
// finishes the launch
public class AccountSwitchingSimulationTests : IDisposable
{
    private const ulong Unsaved = 76561198000000009;

    private readonly AccountSwitchingSetup _setup = new();

    public void Dispose() => _setup.Dispose();

    // Plays out a whole switch: the running Conay hands over and exits, then the new one finishes the switch
    private static async Task<(string[] Args, SimulatedConay Conay, SteamAccounts Accounts)> SwitchVia(
        AccountSwitchingSetup setup, SimulatedConay oldConay, SteamAccounts oldAccounts, ulong steamId,
        string? server = Server)
    {
        Assert.False(await oldAccounts.SwitchTo(steamId, server));
        string[] args = Assert.IsType<string[]>(setup.Steam.HandedOverArgs);
        oldConay.Exit();

        (SimulatedConay conay, SteamAccounts accounts) = setup.StartConay(args);
        await accounts.FinishPendingSwitch();
        return (args, conay, accounts);
    }

    private int HandOvers => _setup.Steam.Events.Count(e => e.StartsWith("hand over"));

    private int Kills => _setup.Steam.Events.Count(e => e == "kill Steam");

    [Fact]
    public async Task Switching_EndsOnTheNewAccountWithTheLaunchCarryingOn()
    {
        _setup.Config.SetServerAccount(Server, Alt);
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();

        (string[] args, SimulatedConay conay, SteamAccounts accounts) =
            await SwitchVia(_setup, oldConay, oldAccounts, Alt);
        (LaunchState state, bool canLaunch) = await _setup.ContinueLaunch(accounts, args);

        Assert.True(canLaunch);
        Assert.Equal(Alt, conay.CurrentSteamId);
        Assert.Equal(Alt, state.SteamAccountId);
        Assert.Equal(Server, state.ServerFile);
        Assert.Equal(1, HandOvers);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task Switching_DoesNotLeaveConanExilesStuckAsRunning()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        (string[] args, SimulatedConay conay, SteamAccounts accounts) =
            await SwitchVia(_setup, oldConay, oldAccounts, Alt);
        await _setup.ContinueLaunch(accounts, args);

        conay.Exit();
        await _setup.Steam.WaitForExitsToBeNoticed();

        Assert.False(_setup.Steam.ConanMarkedRunning);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task TheOldConay_LeavesSteamAlone()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();

        await accounts.SwitchTo(Alt, Server);

        Assert.Equal(0, Kills);
        Assert.True(_setup.Steam.IsRunning);
        Assert.Equal(Main, _setup.Steam.LoggedInAs);
        Assert.Null(_setup.Steam.AutoLoginUser);
        Assert.Equal(Main, _setup.MostRecentAccount());
    }

    [Fact]
    public async Task TheOldConay_SavesConfigAndDisconnectsBeforeHandingOver()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        _setup.Config.SetServerAccount(Server, Alt);
        ulong savedAccount = 0;
        bool? connected = null;
        _setup.Steam.OnHandOver = () =>
        {
            savedAccount = TestServices.CreateConfig(_setup.Files).GetServerAccount(Server);
            connected = conay.IsConnected;
        };

        await accounts.SwitchTo(Alt, Server);

        Assert.Equal(Alt, savedAccount);
        Assert.False(connected);
    }

    [Fact]
    public async Task Steam_IsOnlyClosedOnceTheOldConayIsGone()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();

        await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        List<string> events = _setup.Steam.Events.ToList();
        Assert.InRange(events.IndexOf("Conay exits"), 0, events.IndexOf("kill Steam") - 1);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task Steam_IsOnlyClosedOnceItStoppedTrackingTheOldConay()
    {
        _setup.Steam.ExitNoticeDelay = TimeSpan.FromMilliseconds(300);
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();

        await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        List<string> events = _setup.Steam.Events.ToList();
        Assert.InRange(events.IndexOf("Steam stops tracking Conay"), 0, events.IndexOf("kill Steam") - 1);
        Assert.Equal(Alt, _setup.Steam.LoggedInAs);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task AnotherConayOpen_NewConayLeavesSteamAlone()
    {
        _setup.RunningGameTimeout = TimeSpan.FromMilliseconds(200);
        _setup.StartConay();
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();

        (_, SimulatedConay conay, _) = await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        Assert.Equal(0, Kills);
        Assert.Equal(Main, conay.CurrentSteamId);
        Assert.Contains("Steam still shows Conan Exiles as running! Close it and any other Conay window, then try again.",
            _setup.Statuses);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task AnotherConayClosedWhileWaiting_SwitchCarriesOn()
    {
        (SimulatedConay other, _) = _setup.StartConay();
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        int checks = 0;
        _setup.Steam.OnRunningGameCheck = () =>
        {
            if (++checks == 20)
                other.Exit();
        };

        (_, SimulatedConay conay, _) = await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        List<string> events = _setup.Steam.Events.ToList();
        Assert.InRange(events.LastIndexOf("Conay exits"), 0, events.IndexOf("kill Steam") - 1);
        Assert.Equal(Alt, conay.CurrentSteamId);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task TheNewConay_StaysAwayFromSteamUntilItHasSwitched()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        await oldAccounts.SwitchTo(Alt, Server);
        oldConay.Exit();

        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay(_setup.Steam.HandedOverArgs!);
        int sessionBefore = _setup.Steam.Session;
        bool connectedBefore = conay.IsConnected;
        await accounts.FinishPendingSwitch();

        Assert.False(connectedBefore);
        Assert.True(conay.IsConnected);
        Assert.Equal(sessionBefore + 1, conay.RegisteredSession);
    }

    [Fact]
    public async Task TheNewConay_PreparesTheLoginWhileSteamIsClosed()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();

        await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        Assert.Equal(Alt, _setup.MostRecentAccount());
        Assert.Equal("alice_alt", _setup.Steam.AutoLoginUser);
        Assert.True(File.Exists(_setup.LoginUsersPath + ".bak"));
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task Switching_PassesServerAndAccountToTheNewConay()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();

        await accounts.SwitchTo(Alt, Server);

        Assert.Equal(new[] { "--server", Server, SwitchRequest.SwitchArgument, Alt.ToString() },
            _setup.Steam.HandedOverArgs);
    }

    [Fact]
    public async Task SwitchingWithoutServer_PassesOnlyTheAccount()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();

        await accounts.SwitchTo(Alt);

        Assert.Equal(new[] { SwitchRequest.SwitchArgument, Alt.ToString() }, _setup.Steam.HandedOverArgs);
    }

    [Fact]
    public async Task AlreadyOnTheAccount_LaunchesWithoutRestartingAnything()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();

        Assert.True(await accounts.SwitchTo(Main, Server));

        Assert.Equal(0, HandOvers);
        Assert.Equal(0, Kills);
    }

    [Fact]
    public async Task ConanExilesRunning_RefusesToSwitch()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        conay.IsGameRunning = true;

        Assert.False(await accounts.SwitchTo(Alt, Server));

        Assert.Equal(0, HandOvers);
        Assert.Equal(Main, conay.CurrentSteamId);
        Assert.Contains("Close Conan Exiles before switching Steam accounts!", _setup.Statuses);
    }

    [Fact]
    public async Task ModDownloadsInProgress_FinishBeforeHandingOver()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        conay.ModChecksUntilDownloadsFinish = 2;

        await accounts.SwitchTo(Alt, Server);

        Assert.Equal(2, _setup.Statuses.Count(s => s == "Waiting for mod downloads to finish.."));
        Assert.Equal(1, HandOvers);
    }

    [Fact]
    public async Task HandOverFails_StaysOnTheSameSteam()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        _setup.Steam.HandOverSucceeds = false;

        Assert.False(await accounts.SwitchTo(Alt, Server));

        Assert.Equal(0, Kills);
        Assert.Equal(Main, conay.CurrentSteamId);
        Assert.Contains("Failed to restart Conay! Restart it manually to finish.", _setup.Statuses);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task SteamWontClose_NewConayStaysOnTheOldAccount()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        _setup.Steam.CanShutdown = false;

        (string[] args, SimulatedConay conay, SteamAccounts accounts) =
            await SwitchVia(_setup, oldConay, oldAccounts, Alt);
        (_, bool canLaunch) = await _setup.ContinueLaunch(accounts, args);

        Assert.False(canLaunch);
        Assert.Equal(Main, conay.CurrentSteamId);
        Assert.Contains("Failed to close Steam! Close it manually and try again.", _setup.Statuses);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task AutoLoginFails_NewConayDoesNotSwitchAgain()
    {
        _setup.Steam.ExpiredLogins.Add("alice_alt");
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        (string[] args, SimulatedConay conay, SteamAccounts accounts) =
            await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        // Steam shows its sign-in screen, someone signs in with the wrong account and Conay connects to it
        _setup.Steam.SignIn(Main);
        conay.Resume();
        (_, bool canLaunch) = await _setup.ContinueLaunch(accounts, args);

        Assert.False(canLaunch);
        Assert.Equal(1, HandOvers);
        Assert.Contains(_setup.Statuses, s => s.StartsWith("Steam did not log in as Alt"));
    }

    [Fact]
    public async Task AutoLoginFails_TryingAgainSwitchesNormally()
    {
        _setup.Steam.ExpiredLogins.Add("alice_alt");
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        (string[] args, SimulatedConay conay, SteamAccounts accounts) =
            await SwitchVia(_setup, oldConay, oldAccounts, Alt);
        _setup.Steam.SignIn(Main);
        conay.Resume();
        await _setup.ContinueLaunch(accounts, args);

        Assert.False(await accounts.SwitchTo(Alt, Server));

        Assert.Equal(2, HandOvers);
    }

    [Fact]
    public async Task RestartedForOneAccount_CanStillSwitchToAnother()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        (_, _, SteamAccounts accounts) = await SwitchVia(_setup, oldConay, oldAccounts, Alt);

        Assert.False(await accounts.SwitchTo(Third, Server));

        Assert.Equal(new[] { "--server", Server, SwitchRequest.SwitchArgument, Third.ToString() },
            _setup.Steam.HandedOverArgs);
    }

    [Fact]
    public async Task UnsavedAccount_IsRefusedWithoutTouchingSteam()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();

        Assert.False(await accounts.SwitchTo(Unsaved, Server));

        Assert.Equal(0, HandOvers);
        Assert.Contains(_setup.Statuses, s => s.Contains("is not saved in Steam"));
    }

    [Fact]
    public async Task SecondSwitchWhileSwitching_IsIgnored()
    {
        _setup.ModUpdateCheckInterval = TimeSpan.FromMilliseconds(50);
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        conay.ModChecksUntilDownloadsFinish = 1;

        Task<bool> first = accounts.SwitchTo(Alt, Server);
        bool second = await accounts.SwitchTo(Third, Server);
        await accounts.AddAccount();
        await first;

        Assert.False(second);
        Assert.Equal(0, _setup.Confirmations);
        Assert.Equal(1, HandOvers);
        Assert.Equal(Alt.ToString(), _setup.Steam.HandedOverArgs![^1]);
    }

    [Fact]
    public async Task SwitchWhileFinishingASwitch_IsIgnored()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();
        await oldAccounts.SwitchTo(Alt, Server);
        oldConay.Exit();
        (_, SteamAccounts accounts) = _setup.StartConay(_setup.Steam.HandedOverArgs!);
        _setup.Steam.HoldShutdown = new TaskCompletionSource();

        Task finishing = accounts.FinishPendingSwitch();
        bool switched = await accounts.SwitchTo(Third, Server);
        _setup.Steam.HoldShutdown.SetResult();
        await finishing;

        Assert.False(switched);
        Assert.Equal(1, HandOvers);
    }

    [Fact]
    public async Task SwitchingThereAndBack_EndsWhereItStarted()
    {
        (SimulatedConay first, SteamAccounts firstAccounts) = _setup.StartConay();
        (string[] toAlt, SimulatedConay second, SteamAccounts secondAccounts) =
            await SwitchVia(_setup, first, firstAccounts, Alt);
        await _setup.ContinueLaunch(secondAccounts, toAlt);

        (string[] toMain, SimulatedConay third, SteamAccounts thirdAccounts) =
            await SwitchVia(_setup, second, secondAccounts, Main);
        (_, bool canLaunch) = await _setup.ContinueLaunch(thirdAccounts, toMain);
        third.Exit();
        await _setup.Steam.WaitForExitsToBeNoticed();

        Assert.True(canLaunch);
        Assert.Equal(Main, _setup.Steam.LoggedInAs);
        Assert.Equal(Main, _setup.MostRecentAccount());
        Assert.Equal("alice_main", _setup.Steam.AutoLoginUser);
        Assert.False(_setup.Steam.ConanMarkedRunning);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task SteamNotRunning_SwitchStillWorks()
    {
        using AccountSwitchingSetup setup = new(steamRunning: false);
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = setup.StartConay();

        (string[] args, _, SteamAccounts accounts) = await SwitchVia(setup, oldConay, oldAccounts, Alt);
        (_, bool canLaunch) = await setup.ContinueLaunch(accounts, args);

        Assert.True(canLaunch);
        Assert.Equal(Alt, setup.Steam.LoggedInAs);
        Assert.Empty(setup.Steam.Problems);
    }

    [Fact]
    public async Task FinishPendingSwitch_WithoutOne_LeavesSteamAlone()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();

        await accounts.FinishPendingSwitch();

        Assert.Equal(0, Kills);
        Assert.Equal(Main, conay.CurrentSteamId);
    }

    [Fact]
    public async Task FinishPendingSwitch_ToAnUnsavedAccount_LeavesSteamAlone()
    {
        (SimulatedConay conay, SteamAccounts accounts) =
            _setup.StartConay(SwitchRequest.SwitchArgument, Unsaved.ToString());

        await accounts.FinishPendingSwitch();

        Assert.Equal(0, Kills);
        Assert.Equal(Main, conay.CurrentSteamId);
        Assert.Contains(_setup.Statuses, s => s.Contains("is not saved in Steam"));
    }

    [Fact]
    public async Task FinishPendingSwitch_WhileConanExilesRuns_LeavesSteamAlone()
    {
        (SimulatedConay conay, SteamAccounts accounts) =
            _setup.StartConay(SwitchRequest.SwitchArgument, Alt.ToString());
        conay.IsGameRunning = true;

        await accounts.FinishPendingSwitch();

        Assert.Equal(0, Kills);
        Assert.Contains("Close Conan Exiles before switching Steam accounts!", _setup.Statuses);
    }

    [Fact]
    public async Task AddAccount_Declined_LeavesEverythingAlone()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();
        _setup.ConfirmAnswer = false;

        await accounts.AddAccount();

        Assert.Equal(1, _setup.Confirmations);
        Assert.Equal(0, HandOvers);
    }

    [Fact]
    public async Task AddAccount_RestartsSteamToTheSignInScreen()
    {
        (SimulatedConay oldConay, SteamAccounts oldAccounts) = _setup.StartConay();

        await oldAccounts.AddAccount();
        string[] args = Assert.IsType<string[]>(_setup.Steam.HandedOverArgs);
        oldConay.Exit();
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay(args);
        await accounts.FinishPendingSwitch();

        Assert.Equal(new[] { SwitchRequest.AddAccountArgument }, args);
        Assert.Equal(string.Empty, _setup.Steam.AutoLoginUser);
        Assert.Equal(0UL, _setup.MostRecentAccount());
        Assert.Equal(0UL, _setup.Steam.LoggedInAs);
        Assert.False(conay.IsConnected);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task AddAccount_WhileConanExilesRuns_Refuses()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        conay.IsGameRunning = true;

        await accounts.AddAccount();

        Assert.Equal(0, HandOvers);
        Assert.Contains("Close Conan Exiles before switching Steam accounts!", _setup.Statuses);
    }

    [Fact]
    public async Task ChooseAccount_AsksWithSavedAccountsAndTheCurrentOne()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();
        _setup.PickerAnswer = saved => saved.Single(a => a.SteamId == Alt);

        ulong? chosen = await accounts.ChooseAccount("Arcana's Grasp RP");

        Assert.Equal(Alt, chosen);
        (string message, IReadOnlyList<SteamAccount> offered, ulong current) = Assert.Single(_setup.PickerQuestions);
        Assert.Contains("Arcana's Grasp RP", message);
        Assert.Equal(new[] { Alt, Main, Third }, offered.Select(a => a.SteamId));
        Assert.Equal(Main, current);
    }

    [Fact]
    public async Task ChooseAccount_Cancelled_ReturnsNothing()
    {
        (_, SteamAccounts accounts) = _setup.StartConay();

        Assert.Null(await accounts.ChooseAccount("Arcana's Grasp RP"));
    }
}
