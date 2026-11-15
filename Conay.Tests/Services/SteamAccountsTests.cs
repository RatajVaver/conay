using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using Conay.Utils;

namespace Conay.Tests.Services;

public class SteamAccountsTests : IDisposable
{
    private const ulong Main = 76561198000000001;
    private const ulong Alt = 76561198000000002;
    private const ulong Third = 76561198000000003;

    private readonly TempDirectory _dir = new();
    private readonly LauncherConfig _config;

    public SteamAccountsTests()
    {
        _config = TestServices.CreateConfig(_dir);
    }

    public void Dispose() => _dir.Dispose();

    private string SteamRoot => _dir.PathOf("steam");

    private string WriteLoginUsers(string contents) => _dir.Write("steam/config/loginusers.vdf", contents);

    private SteamAccounts CreateAccounts(string? steamRoot = null) =>
        TestServices.CreateSteamAccounts(_config, steamRoot ?? SteamRoot);

    [Fact]
    public void GetSavedAccounts_WithoutSteamFolder_ReturnsNothing()
    {
        SteamAccounts accounts = TestServices.CreateSteamAccounts(_config, null);

        Assert.Empty(accounts.GetSavedAccounts());
    }

    [Fact]
    public void GetSavedAccounts_WithoutLoginUsersFile_ReturnsNothing()
    {
        Assert.Empty(CreateAccounts().GetSavedAccounts());
    }

    [Fact]
    public void GetSavedAccounts_ReadsAccountDetails()
    {
        WriteLoginUsers(LoginUsers.File(
            LoginUsers.User(Main, "alice_main", "Alice", rememberPassword: true, mostRecent: true)));

        SteamAccount account = Assert.Single(CreateAccounts().GetSavedAccounts());

        Assert.Equal(Main, account.SteamId);
        Assert.Equal("alice_main", account.AccountName);
        Assert.Equal("Alice", account.PersonaName);
        Assert.True(account.RememberPassword);
        Assert.True(account.MostRecent);
        Assert.Equal(Path.Combine(SteamRoot, "config", "avatarcache", $"{Main}.png"), account.AvatarPath);
    }

    [Fact]
    public void GetSavedAccounts_ReadsFlagsThatAreOff()
    {
        WriteLoginUsers(LoginUsers.File(LoginUsers.User(Main, "alice_main", rememberPassword: false)));

        SteamAccount account = Assert.Single(CreateAccounts().GetSavedAccounts());

        Assert.False(account.RememberPassword);
        Assert.False(account.MostRecent);
    }

    [Fact]
    public void GetSavedAccounts_SortsByDisplayNameIgnoringCase()
    {
        WriteLoginUsers(LoginUsers.File(
            LoginUsers.User(Main, "zed_login", "zed"),
            LoginUsers.User(Alt, "alpha_login", "Alpha"),
            LoginUsers.User(Third, "beta")));

        IReadOnlyList<SteamAccount> accounts = CreateAccounts().GetSavedAccounts();

        Assert.Equal(new[] { "Alpha", "beta", "zed" }, accounts.Select(a => a.DisplayName));
    }

    [Fact]
    public void GetSavedAccounts_SkipsInvalidEntries()
    {
        WriteLoginUsers("\"users\"\n{\n" +
                        "\t\"not-a-steam-id\"\n\t{\n\t\t\"AccountName\"\t\t\"broken\"\n\t}\n" +
                        "\t\"76561198000000002\"\n\t{\n\t\t\"PersonaName\"\t\t\"No login\"\n\t}\n" +
                        LoginUsers.User(Main, "alice_main") +
                        "}\n");

        SteamAccount account = Assert.Single(CreateAccounts().GetSavedAccounts());

        Assert.Equal(Main, account.SteamId);
    }

    [Fact]
    public void GetSavedAccounts_CorruptFile_ReturnsNothing()
    {
        WriteLoginUsers("\"users\"\n{\n\t\"76561198000000001\"\n\t{\n");

        Assert.Empty(CreateAccounts().GetSavedAccounts());
    }

    [Fact]
    public void GetSavedAccounts_ReusesListUntilFileChanges()
    {
        string path = WriteLoginUsers(LoginUsers.File(LoginUsers.User(Main, "alice_main")));
        SteamAccounts accounts = CreateAccounts();

        IReadOnlyList<SteamAccount> first = accounts.GetSavedAccounts();
        IReadOnlyList<SteamAccount> second = accounts.GetSavedAccounts();

        File.WriteAllText(path, LoginUsers.File(LoginUsers.User(Main, "alice_main"), LoginUsers.User(Alt, "alice_alt")));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        IReadOnlyList<SteamAccount> afterChange = accounts.GetSavedAccounts();

        Assert.Same(first, second);
        Assert.NotSame(first, afterChange);
        Assert.Equal(2, afterChange.Count);
    }

    [Fact]
    public async Task ChooseAccount_WithoutMultipleAccounts_KeepsCurrentWithoutAsking()
    {
        WriteLoginUsers(LoginUsers.File(LoginUsers.User(Main, "alice_main")));

        Assert.Equal(0UL, await CreateAccounts().ChooseAccount("Test Server"));
    }

    [Fact]
    public async Task ChooseAccount_WithoutSavedAccounts_KeepsCurrentWithoutAsking()
    {
        Assert.Equal(0UL, await CreateAccounts().ChooseAccount("Test Server"));
    }

    [Fact]
    public void ApplyLogin_MarksTargetForAutomaticLogin()
    {
        VdfNode vdf = VdfNode.Parse(LoginUsers.File(
            LoginUsers.User(Main, "alice_main", mostRecent: true),
            LoginUsers.User(Alt, "alice_alt", rememberPassword: false)));

        SteamAccounts.ApplyLogin(vdf, Alt);

        VdfNode alt = vdf.GetNode("users")!.GetNode(Alt.ToString())!;
        Assert.Equal("1", alt.GetString("MostRecent"));
        Assert.Equal("1", alt.GetString("RememberPassword"));
        Assert.Equal("1", alt.GetString("AllowAutoLogin"));
    }

    [Fact]
    public void ApplyLogin_LeavesOtherAccountsLoginsAlone()
    {
        VdfNode vdf = VdfNode.Parse(LoginUsers.File(
            LoginUsers.User(Main, "alice_main", rememberPassword: false, mostRecent: true),
            LoginUsers.User(Alt, "alice_alt")));

        SteamAccounts.ApplyLogin(vdf, Alt);

        VdfNode main = vdf.GetNode("users")!.GetNode(Main.ToString())!;
        Assert.Equal("0", main.GetString("MostRecent"));
        Assert.Equal("0", main.GetString("RememberPassword"));
        Assert.Null(main.GetString("AllowAutoLogin"));
        Assert.Equal("1760000000", main.GetString("Timestamp"));
    }

    [Fact]
    public void ApplyLogin_KeepsSteamsKeyCasing()
    {
        VdfNode vdf = VdfNode.Parse("\"users\"\n{\n\t\"76561198000000002\"\n\t{\n" +
                                    "\t\t\"AccountName\"\t\t\"alice_alt\"\n\t\t\"mostrecent\"\t\t\"0\"\n\t}\n}\n");

        SteamAccounts.ApplyLogin(vdf, Alt);

        Assert.Contains("\"mostrecent\"\t\t\"1\"", vdf.ToString());
    }

    [Fact]
    public void ApplyLogin_WithoutAccount_LeavesNoAccountForAutomaticLogin()
    {
        VdfNode vdf = VdfNode.Parse(LoginUsers.File(
            LoginUsers.User(Main, "alice_main", mostRecent: true),
            LoginUsers.User(Alt, "alice_alt")));

        SteamAccounts.ApplyLogin(vdf, 0);

        foreach ((string _, VdfNode user) in vdf.GetNode("users")!.Children)
        {
            Assert.Equal("0", user.GetString("MostRecent"));
            Assert.Null(user.GetString("AllowAutoLogin"));
        }
    }

    [Fact]
    public void ApplyLogin_WithoutUsers_Throws()
    {
        Assert.Throws<InvalidDataException>(() => SteamAccounts.ApplyLogin(VdfNode.Parse("\"other\"\n{\n}\n"), Alt));
    }
}
