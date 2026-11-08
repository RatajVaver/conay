using System.Diagnostics;
using Conay.Services;
using Conay.Tests.Utils;

namespace Conay.Tests.Services;

// How Conay starts Steam (when it isn't running, and after switching accounts)
public class SteamClientStartTests
{
    private const string SteamExe = @"C:\Program Files (x86)\Steam\steam.exe";

    [Fact]
    public void CreateClientStartInfo_StartsSteamLikeItsStartedByHand()
    {
        ProcessStartInfo startInfo = Steam.CreateClientStartInfo(SteamExe, StartupEnvironmentTests.Startup);

        Assert.Equal(SteamExe, startInfo.FileName);
        Assert.Empty(startInfo.ArgumentList);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal(@"C:\Program Files (x86)\Steam", startInfo.WorkingDirectory);
    }

    // Steam started through a steam:// link inherited this process's environment, which can't be changed then
    [Fact]
    public void CreateClientStartInfo_StartsSteamDirectly()
    {
        ProcessStartInfo startInfo = Steam.CreateClientStartInfo(SteamExe, StartupEnvironmentTests.Startup);

        Assert.False(startInfo.UseShellExecute);
    }

    // Not the game settings connecting to Steam as Conan Exiles put into Conay's environment
    [Fact]
    public void CreateClientStartInfo_GivesSteamTheEnvironmentConayStartedWith()
    {
        ProcessStartInfo startInfo = Steam.CreateClientStartInfo(SteamExe, StartupEnvironmentTests.Startup);

        Assert.Equal(StartupEnvironmentTests.Startup.Keys.Order(StringComparer.OrdinalIgnoreCase),
            startInfo.Environment.Keys.Order(StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(startInfo.Environment.Keys,
            name => StartupEnvironmentTests.ConanExilesGameEnvironment.ContainsKey(name));
    }
}
