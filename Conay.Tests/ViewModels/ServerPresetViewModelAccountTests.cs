using Avalonia.Controls;
using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using Conay.ViewModels.Parts;

namespace Conay.Tests.ViewModels;

// The Steam account button and menu on server cards
public class ServerPresetViewModelAccountTests : IDisposable
{
    private const string Server = "arcanasgrasp";
    private const ulong Main = 76561198000000001;
    private const ulong Alt = 76561198000000002;

    private readonly TempDirectory _dir = new();
    private readonly LauncherConfig _config;

    public ServerPresetViewModelAccountTests()
    {
        _config = TestServices.CreateConfig(_dir);
    }

    public void Dispose() => _dir.Dispose();

    private void SaveAccounts(params string[] users) =>
        _dir.Write("steam/config/loginusers.vdf", LoginUsers.File(users));

    private void SaveMainAndAlt() => SaveAccounts(
        LoginUsers.User(Main, "alice_main", "Main"),
        LoginUsers.User(Alt, "alice_alt", "Alt"));

    // The Steam service is only needed to launch the server, which these tests never do
    private ServerPresetViewModel CreateCard()
    {
        SteamAccounts accounts = TestServices.CreateSteamAccounts(_config, _dir.PathOf("steam"));
        ServerInfo info = new()
        {
            File = Server,
            Name = "Arcana's Grasp RP",
            Ip = "127.0.0.1:7777",
            Provider = new FakePresetService()
        };

        return new ServerPresetViewModel(new Router(new LaunchState(), _config), null!, accounts, _config, info);
    }

    private static SteamAccountOption Option(ServerPresetViewModel card, string name)
    {
        card.RefreshAccountOptions();
        return card.AccountOptions.Single(o => o.Name == name);
    }

    [Fact]
    public void AccountButton_IsHiddenWithOneSavedAccount()
    {
        SaveAccounts(LoginUsers.User(Main, "alice_main", "Main"));

        Assert.False(CreateCard().ShowAccountButton);
    }

    [Fact]
    public void AccountButton_IsShownWithMultipleSavedAccounts()
    {
        SaveMainAndAlt();

        ServerPresetViewModel card = CreateCard();

        Assert.True(card.ShowAccountButton);
        Assert.Null(card.AccountLabel);
        Assert.False(card.HasAccountLabel);
        Assert.Equal("Choose a Steam account for this server", card.AccountTooltip);
    }

    [Fact]
    public void AssignedAccount_ShowsItsName()
    {
        SaveMainAndAlt();
        _config.SetServerAccount(Server, Alt);

        ServerPresetViewModel card = CreateCard();

        Assert.Equal("Alt", card.AccountLabel);
        Assert.True(card.HasAccountLabel);
        Assert.Equal("Launches with Steam account: Alt", card.AccountTooltip);
    }

    [Fact]
    public void AssignedAccount_KeepsButtonVisibleWithOneSavedAccount()
    {
        SaveAccounts(LoginUsers.User(Alt, "alice_alt", "Alt"));
        _config.SetServerAccount(Server, Alt);

        Assert.True(CreateCard().ShowAccountButton);
    }

    [Fact]
    public void AssignedAccountNoLongerSaved_ShowsSteamId()
    {
        SaveAccounts(LoginUsers.User(Main, "alice_main", "Main"));
        _config.SetServerAccount(Server, Alt);

        Assert.Equal(Alt.ToString(), CreateCard().AccountLabel);
    }

    [Fact]
    public void AskEveryTime_ShowsAsk()
    {
        SaveMainAndAlt();
        _config.SetServerAccount(Server, 0, askEveryTime: true);

        ServerPresetViewModel card = CreateCard();

        Assert.Equal("Ask", card.AccountLabel);
        Assert.True(card.ShowAccountButton);
        Assert.Equal("Asks which Steam account to use on every launch", card.AccountTooltip);
    }

    [Fact]
    public void AccountOptions_ListChoicesInOrder()
    {
        SaveMainAndAlt();
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        Assert.Equal(
            new[]
            {
                "Current Steam account", "Ask every time", "Alt (alice_alt)", "Main (alice_main)", "Add account…"
            },
            card.AccountOptions.Select(o => o.Name));
    }

    [Fact]
    public void AccountOptions_ShowLoginNameOnlyWhenItDiffersFromPersona()
    {
        SaveAccounts(
            LoginUsers.User(Main, "alice_main", "alice_main"),
            LoginUsers.User(Alt, "alice_alt"));
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        Assert.Contains(card.AccountOptions, o => o.Name == "alice_main");
        Assert.Contains(card.AccountOptions, o => o.Name == "alice_alt");
    }

    [Fact]
    public void AccountOptions_CheckCurrentAccountByDefault()
    {
        SaveMainAndAlt();
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        Assert.Equal(new[] { "Current Steam account" }, card.AccountOptions.Where(o => o.IsSelected).Select(o => o.Name));
    }

    [Fact]
    public void AccountOptions_CheckAssignedAccount()
    {
        SaveMainAndAlt();
        _config.SetServerAccount(Server, Alt);
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        Assert.Equal(new[] { "Alt (alice_alt)" }, card.AccountOptions.Where(o => o.IsSelected).Select(o => o.Name));
    }

    [Fact]
    public void AccountOptions_CheckAskEveryTime()
    {
        SaveMainAndAlt();
        _config.SetServerAccount(Server, 0, askEveryTime: true);
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        Assert.Equal(new[] { "Ask every time" }, card.AccountOptions.Where(o => o.IsSelected).Select(o => o.Name));
    }

    [Fact]
    public void AccountOptions_KeepAssignedAccountThatIsNoLongerSaved()
    {
        SaveMainAndAlt();
        const ulong removed = 76561198000000009;
        _config.SetServerAccount(Server, removed);
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        SteamAccountOption option = Assert.Single(card.AccountOptions, o => o.IsSelected);
        Assert.Equal($"Unknown account ({removed})", option.Name);
    }

    [Fact]
    public void AccountOptions_AddAccountIsAnActionNotAChoice()
    {
        SaveMainAndAlt();
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();

        SteamAccountOption addAccount = card.AccountOptions.Last();
        Assert.Equal("Add account…", addAccount.Name);
        Assert.Equal(MenuItemToggleType.None, addAccount.ToggleType);
        Assert.All(card.AccountOptions.SkipLast(1), o => Assert.Equal(MenuItemToggleType.Radio, o.ToggleType));
    }

    [Fact]
    public void AccountOptions_RefreshReplacesPreviousOptions()
    {
        SaveMainAndAlt();
        ServerPresetViewModel card = CreateCard();

        card.RefreshAccountOptions();
        card.RefreshAccountOptions();

        Assert.Equal(5, card.AccountOptions.Count);
    }

    [Fact]
    public void ChoosingAccount_AssignsIt()
    {
        SaveMainAndAlt();
        ServerPresetViewModel card = CreateCard();

        Option(card, "Alt (alice_alt)").SelectCommand.Execute(null);

        Assert.Equal(Alt, _config.GetServerAccount(Server));
        Assert.Equal("Alt", card.AccountLabel);
    }

    [Fact]
    public void ChoosingAskEveryTime_ReplacesAssignedAccount()
    {
        SaveMainAndAlt();
        _config.SetServerAccount(Server, Alt);
        ServerPresetViewModel card = CreateCard();

        Option(card, "Ask every time").SelectCommand.Execute(null);

        Assert.True(_config.AsksForServerAccount(Server));
        Assert.Equal(0UL, _config.GetServerAccount(Server));
        Assert.Equal("Ask", card.AccountLabel);
    }

    [Fact]
    public void ChoosingCurrentAccount_ClearsAssignment()
    {
        SaveMainAndAlt();
        _config.SetServerAccount(Server, Alt);
        ServerPresetViewModel card = CreateCard();

        Option(card, "Current Steam account").SelectCommand.Execute(null);

        Assert.Equal(0UL, _config.GetServerAccount(Server));
        Assert.False(_config.AsksForServerAccount(Server));
        Assert.Null(card.AccountLabel);
    }

    [Fact]
    public void ChoosingAccount_UpdatesCheckedOptionOnNextOpen()
    {
        SaveMainAndAlt();
        ServerPresetViewModel card = CreateCard();

        Option(card, "Main (alice_main)").SelectCommand.Execute(null);
        card.RefreshAccountOptions();

        Assert.Equal(new[] { "Main (alice_main)" }, card.AccountOptions.Where(o => o.IsSelected).Select(o => o.Name));
    }
}
