using Conay.Services;
using Conay.Tests.TestSupport;

namespace Conay.Tests.Services;

public class LauncherConfigTests : IDisposable
{
    private const ulong Main = 76561198000000001;
    private const ulong Alt = 76561198000000002;

    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private LauncherConfig CreateConfig() => TestServices.CreateConfig(_dir);

    [Fact]
    public void GetServerAccount_WithoutAssignment_ReturnsZero()
    {
        LauncherConfig config = CreateConfig();

        Assert.Equal(0UL, config.GetServerAccount("arcanasgrasp"));
        Assert.False(config.AsksForServerAccount("arcanasgrasp"));
    }

    [Fact]
    public void SetServerAccount_AssignsAccount()
    {
        LauncherConfig config = CreateConfig();

        config.SetServerAccount("arcanasgrasp", Alt);

        Assert.Equal(Alt, config.GetServerAccount("arcanasgrasp"));
        Assert.False(config.AsksForServerAccount("arcanasgrasp"));
    }

    [Fact]
    public void SetServerAccount_ReplacesPreviousAccount()
    {
        LauncherConfig config = CreateConfig();
        config.SetServerAccount("arcanasgrasp", Main);

        config.SetServerAccount("arcanasgrasp", Alt);

        Assert.Equal(Alt, config.GetServerAccount("arcanasgrasp"));
    }

    [Fact]
    public void SetServerAccount_WithZero_RemovesAssignment()
    {
        LauncherConfig config = CreateConfig();
        config.SetServerAccount("arcanasgrasp", Alt);

        config.SetServerAccount("arcanasgrasp", 0);

        Assert.Equal(0UL, config.GetServerAccount("arcanasgrasp"));
        Assert.DoesNotContain("arcanasgrasp", config.Data.ServerAccounts.Keys);
    }

    [Fact]
    public void SetServerAccount_AskEveryTime_ReplacesAssignedAccount()
    {
        LauncherConfig config = CreateConfig();
        config.SetServerAccount("arcanasgrasp", Alt);

        config.SetServerAccount("arcanasgrasp", 0, askEveryTime: true);

        Assert.True(config.AsksForServerAccount("arcanasgrasp"));
        Assert.Equal(0UL, config.GetServerAccount("arcanasgrasp"));
    }

    [Fact]
    public void SetServerAccount_Account_ReplacesAskEveryTime()
    {
        LauncherConfig config = CreateConfig();
        config.SetServerAccount("arcanasgrasp", 0, askEveryTime: true);

        config.SetServerAccount("arcanasgrasp", Alt);

        Assert.False(config.AsksForServerAccount("arcanasgrasp"));
        Assert.Equal(Alt, config.GetServerAccount("arcanasgrasp"));
    }

    [Fact]
    public void SetServerAccount_WithZero_StopsAskingEveryTime()
    {
        LauncherConfig config = CreateConfig();
        config.SetServerAccount("arcanasgrasp", 0, askEveryTime: true);

        config.SetServerAccount("arcanasgrasp", 0);

        Assert.False(config.AsksForServerAccount("arcanasgrasp"));
        Assert.Empty(config.Data.AskAccountServers);
    }

    [Fact]
    public void SetServerAccount_KeepsServersIndependent()
    {
        LauncherConfig config = CreateConfig();

        config.SetServerAccount("arcanasgrasp", Alt);
        config.SetServerAccount("roguepleasures", 0, askEveryTime: true);

        Assert.Equal(Alt, config.GetServerAccount("arcanasgrasp"));
        Assert.False(config.AsksForServerAccount("arcanasgrasp"));
        Assert.Equal(0UL, config.GetServerAccount("roguepleasures"));
        Assert.True(config.AsksForServerAccount("roguepleasures"));
    }

    [Fact]
    public void SaveConfig_PersistsServerAccounts()
    {
        // Set directly, SetServerAccount also queues a delayed save that could race the reload below
        LauncherConfig config = CreateConfig();
        config.Data.ServerAccounts["arcanasgrasp"] = Alt;
        config.Data.AskAccountServers.Add("roguepleasures");

        config.SaveConfig();
        LauncherConfig reloaded = CreateConfig();

        Assert.Equal(Alt, reloaded.GetServerAccount("arcanasgrasp"));
        Assert.True(reloaded.AsksForServerAccount("roguepleasures"));
    }

    [Fact]
    public void Constructor_WithUnreadableConfig_UsesDefaults()
    {
        _dir.Write("config.json", "{ not json");

        LauncherConfig config = CreateConfig();

        Assert.Empty(config.Data.ServerAccounts);
        Assert.Empty(config.Data.AskAccountServers);
    }
}
