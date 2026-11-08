using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using Conay.ViewModels.Parts;
using static Conay.Tests.TestSupport.AccountSwitchingSetup;

namespace Conay.Tests.ViewModels;

// Pressing play on a server card, and which Steam account the launch ends up using
public class ServerPresetViewModelLaunchTests : IDisposable
{
    private readonly AccountSwitchingSetup _setup = new();
    private readonly LaunchState _state = new();
    private readonly FakePresetService _provider = new();
    private int _launches;

    public void Dispose() => _setup.Dispose();

    private (ServerPresetViewModel Card, SteamAccounts Accounts) CreateCard()
    {
        (SimulatedConay conay, SteamAccounts accounts) = _setup.StartConay();
        Router router = new(_state, _setup.Config);
        router.ShowLaunchForPreset += _ => _launches++;

        ServerInfo info = new()
        {
            File = Server,
            Name = "Arcana's Grasp RP",
            Ip = "127.0.0.1:7777",
            Provider = _provider
        };

        return (new ServerPresetViewModel(router, conay, accounts, _setup.Config, info), accounts);
    }

    [Fact]
    public async Task AskEveryTime_LaunchesWithTheChosenAccount()
    {
        _setup.Config.SetServerAccount(Server, 0, askEveryTime: true);
        _setup.PickerAnswer = saved => saved.Single(a => a.SteamId == Alt);
        (ServerPresetViewModel card, _) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.Equal(1, _launches);
        Assert.Equal(Alt, _state.SteamAccountId);
        Assert.Equal(Server, _state.ServerFile);
        Assert.Equal(new[] { Server }, _provider.SavedModlists);
        Assert.Contains("Arcana's Grasp RP", Assert.Single(_setup.PickerQuestions).Message);
    }

    [Fact]
    public async Task AskEveryTime_Cancelled_DoesNotLaunch()
    {
        _setup.Config.SetServerAccount(Server, 0, askEveryTime: true);
        (ServerPresetViewModel card, _) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.Equal(0, _launches);
        Assert.Empty(_provider.SavedModlists);
    }

    [Fact]
    public async Task AskEveryTime_ChoosingTheCurrentAccount_LaunchesWithoutSwitching()
    {
        _setup.Config.SetServerAccount(Server, 0, askEveryTime: true);
        _setup.PickerAnswer = saved => saved.Single(a => a.SteamId == Main);
        (ServerPresetViewModel card, SteamAccounts accounts) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.True(await accounts.SwitchTo(_state.SteamAccountId, _state.ServerFile));
        Assert.DoesNotContain("kill Steam", _setup.Steam.Events);
    }

    [Fact]
    public async Task AskEveryTime_ChoosingAnotherAccount_SwitchesToIt()
    {
        _setup.Config.SetServerAccount(Server, 0, askEveryTime: true);
        _setup.PickerAnswer = saved => saved.Single(a => a.SteamId == Alt);
        (ServerPresetViewModel card, SteamAccounts accounts) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.False(await accounts.SwitchTo(_state.SteamAccountId, _state.ServerFile));
        Assert.Equal(new[] { "--server", Server, SwitchRequest.SwitchArgument, Alt.ToString() },
            _setup.Steam.HandedOverArgs);
        Assert.Empty(_setup.Steam.Problems);
    }

    [Fact]
    public async Task AskEveryTime_WithOneSavedAccount_DoesNotAsk()
    {
        _setup.Files.Write("steam/config/loginusers.vdf",
            LoginUsers.File(LoginUsers.User(Main, "alice_main", "Main", mostRecent: true)));
        _setup.Config.SetServerAccount(Server, 0, askEveryTime: true);
        (ServerPresetViewModel card, _) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.Empty(_setup.PickerQuestions);
        Assert.Equal(1, _launches);
        Assert.Equal(0UL, _state.SteamAccountId);
    }

    [Fact]
    public async Task AssignedAccount_LaunchesWithoutAsking()
    {
        _setup.Config.SetServerAccount(Server, Alt);
        (ServerPresetViewModel card, _) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.Empty(_setup.PickerQuestions);
        Assert.Equal(Alt, _state.SteamAccountId);
    }

    [Fact]
    public async Task NoAccountChosen_LaunchesOnTheCurrentAccount()
    {
        (ServerPresetViewModel card, _) = CreateCard();

        await card.LaunchServerPresetCommand.ExecuteAsync(null);

        Assert.Empty(_setup.PickerQuestions);
        Assert.Equal(1, _launches);
        Assert.Equal(0UL, _state.SteamAccountId);
    }
}
