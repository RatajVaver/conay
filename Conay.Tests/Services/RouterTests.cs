using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;

namespace Conay.Tests.Services;

public class RouterTests : IDisposable
{
    private const ulong Main = 76561198000000001;
    private const ulong Alt = 76561198000000002;

    private readonly TempDirectory _dir = new();
    private readonly LauncherConfig _config;
    private readonly LaunchState _state = new();
    private readonly Router _router;

    public RouterTests()
    {
        _config = TestServices.CreateConfig(_dir);
        _router = new Router(_state, _config);
    }

    public void Dispose() => _dir.Dispose();

    private static ServerData Preset(string? fileName = "arcanasgrasp") => new()
    {
        Name = "Arcana's Grasp RP",
        Ip = "127.0.0.1:7777",
        FileName = fileName
    };

    [Fact]
    public void ReadyForLaunch_UsesAccountAssignedToServer()
    {
        _config.SetServerAccount("arcanasgrasp", Alt);

        _router.ReadyForLaunch(Preset());

        Assert.Equal(Alt, _state.SteamAccountId);
    }

    [Fact]
    public void ReadyForLaunch_WithoutAssignment_StaysOnCurrentAccount()
    {
        _router.ReadyForLaunch(Preset());

        Assert.Equal(0UL, _state.SteamAccountId);
    }

    [Fact]
    public void ReadyForLaunch_ChosenAccountOverridesAssignment()
    {
        _config.SetServerAccount("arcanasgrasp", Main);

        _router.ReadyForLaunch(Preset(), steamAccountId: Alt);

        Assert.Equal(Alt, _state.SteamAccountId);
    }

    [Fact]
    public void ReadyForLaunch_ChoosingCurrentAccountOverridesAssignment()
    {
        _config.SetServerAccount("arcanasgrasp", Main);

        _router.ReadyForLaunch(Preset(), steamAccountId: 0);

        Assert.Equal(0UL, _state.SteamAccountId);
    }

    [Fact]
    public void ReadyForLaunch_AskEveryTimeServerWithoutChoice_StaysOnCurrentAccount()
    {
        _config.SetServerAccount("arcanasgrasp", 0, askEveryTime: true);

        _router.ReadyForLaunch(Preset());

        Assert.Equal(0UL, _state.SteamAccountId);
    }

    [Fact]
    public void ReadyForLaunch_RemembersServerFile()
    {
        _router.ReadyForLaunch(Preset());

        Assert.Equal("arcanasgrasp", _state.ServerFile);
    }

    [Fact]
    public void ReadyForLaunch_PresetWithoutFile_HasNoServerOrAccount()
    {
        _router.ReadyForLaunch(Preset());

        _router.ReadyForLaunch(Preset(fileName: null), isSaveLaunch: true);

        Assert.Null(_state.ServerFile);
        Assert.Equal(0UL, _state.SteamAccountId);
        Assert.True(_state.IsSaveLaunch);
    }

    [Fact]
    public void ReadyForLaunch_WithoutPreset_ClearsPreviousServer()
    {
        _config.SetServerAccount("arcanasgrasp", Alt);
        _router.ReadyForLaunch(Preset());

        _router.ReadyForLaunch(null);

        Assert.Null(_state.ServerFile);
        Assert.Equal(0UL, _state.SteamAccountId);
        Assert.Equal(string.Empty, _state.Name);
    }

    [Fact]
    public void ReadyForLaunch_ShowsLaunchPageForPreset()
    {
        ServerData preset = Preset();
        ServerData? shown = null;
        _router.ShowLaunchForPreset += p => shown = p;

        _router.ReadyForLaunch(preset);

        Assert.Same(preset, shown);
    }

    [Fact]
    public void ReadyForLaunch_RecordsServerInHistory()
    {
        _router.ReadyForLaunch(Preset());

        Assert.Equal("arcanasgrasp", _config.Data.History[0]);
    }
}
