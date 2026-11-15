using Conay.Data;
using Conay.Services;
using Conay.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conay.Tests.TestSupport;

// A machine with three Steam accounts saved, a simulated Steam and Conay instances that can be started on it
internal sealed class AccountSwitchingSetup : IDisposable
{
    public const ulong Main = 76561198000000001;
    public const ulong Alt = 76561198000000002;
    public const ulong Third = 76561198000000003;
    public const string Server = "arcanasgrasp";

    public AccountSwitchingSetup(ulong loggedInAs = Main, bool steamRunning = true)
    {
        Files.Write("steam/config/loginusers.vdf", LoginUsers.File(
            LoginUsers.User(Main, "alice_main", "Main", mostRecent: loggedInAs == Main),
            LoginUsers.User(Alt, "alice_alt", "Alt", mostRecent: loggedInAs == Alt),
            LoginUsers.User(Third, "alice_third", "Third", mostRecent: loggedInAs == Third)));

        Config = TestServices.CreateConfig(Files);
        Steam = new SimulatedSteam(SteamRoot, loggedInAs, steamRunning);
    }

    public TempDirectory Files { get; } = new();
    public LauncherConfig Config { get; }
    public SimulatedSteam Steam { get; }
    public List<string> Statuses { get; } = [];
    public TimeSpan ModUpdateCheckInterval { get; set; } = TimeSpan.Zero;

    // How long a new Conay waits for Steam to stop showing Conan Exiles as running before it gives up on switching
    public TimeSpan RunningGameTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // What the "which account?" picker answers, and how it was asked
    public Func<IReadOnlyList<SteamAccount>, SteamAccount?> PickerAnswer { get; set; } = _ => null;
    public List<(string Message, IReadOnlyList<SteamAccount> Accounts, ulong Current)> PickerQuestions { get; } = [];

    // What the "add account" confirmation answers, and how often it was asked
    public bool ConfirmAnswer { get; set; } = true;
    public int Confirmations { get; private set; }

    public string SteamRoot => Files.PathOf("steam");
    public string LoginUsersPath => Path.Combine(SteamRoot, "config", "loginusers.vdf");

    // Starts a Conay instance with the given command line. Like the Steam service, it connects to Steam right
    // away unless it was started to finish a switch.
    public (SimulatedConay Conay, SteamAccounts Accounts) StartConay(params string[] args)
    {
        SwitchRequest? pendingSwitch = SwitchRequest.FromArgs(args);
        SimulatedConay conay = new(Steam, connect: pendingSwitch == null);

        NotifyService notifyService = new();
        notifyService.StatusChanged += (_, status) => Statuses.Add(status);

        SteamAccounts accounts = new(NullLogger<SteamAccounts>.Instance, conay, Steam, Config, notifyService)
        {
            LocateSteamRoot = () => SteamRoot,
            PendingSwitch = pendingSwitch,
            CurrentAccountTimeout = TimeSpan.Zero,
            ModUpdateCheckInterval = ModUpdateCheckInterval,
            RunningGameTimeout = RunningGameTimeout,
            RunningGameCheckInterval = TimeSpan.FromMilliseconds(5),
            Confirm = _ =>
            {
                Confirmations++;
                return Task.FromResult(ConfirmAnswer);
            },
            PickAccount = (message, accountList, current) =>
            {
                PickerQuestions.Add((message, accountList, current));
                return Task.FromResult(PickerAnswer(accountList));
            }
        };

        return (conay, accounts);
    }

    // What a Conay started with the handed over arguments does after finishing the switch: routes the --server
    // launch, then checks it's on the right account
    public async Task<(LaunchState State, bool CanLaunch)> ContinueLaunch(SteamAccounts accounts, string[] args)
    {
        int serverIndex = Array.IndexOf(args, "--server");
        string? server = serverIndex >= 0 ? args[serverIndex + 1] : null;
        ulong switchedTo = SwitchRequest.FromArgs(args)?.SteamId ?? 0;

        LaunchState state = new();
        Router router = new(state, Config);
        router.ReadyForLaunch(new ServerData { Name = "Arcana's Grasp RP", Ip = "127.0.0.1:7777", FileName = server },
            steamAccountId: switchedTo != 0 ? switchedTo : null);

        bool canLaunch = state.SteamAccountId == 0 ||
                         await accounts.SwitchTo(state.SteamAccountId, state.ServerFile);
        return (state, canLaunch);
    }

    // The account Steam will log into next according to loginusers.vdf
    public ulong MostRecentAccount()
    {
        VdfNode users = VdfNode.Parse(File.ReadAllText(LoginUsersPath)).GetNode("users")!;
        return users.Children.Where(u => u.Node.GetString("MostRecent") == "1")
            .Select(u => ulong.Parse(u.Key))
            .SingleOrDefault();
    }

    public void Dispose() => Files.Dispose();
}
