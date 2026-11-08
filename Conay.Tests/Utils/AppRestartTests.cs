using System.Diagnostics;
using Conay.Utils;

namespace Conay.Tests.Utils;

public class AppRestartTests
{
    [Fact]
    public void BuildCommand_PassesArgumentsAndPreviousInstance()
    {
        List<string> command = AppRestart.BuildCommand("/opt/conay/Conay", "/opt/conay/Conay.dll",
            ["--server", "arcanasgrasp"], 1234);

        Assert.Equal(new[] { "/opt/conay/Conay", "--server", "arcanasgrasp", "--restarted-from", "1234" }, command);
    }

    [Fact]
    public void BuildCommand_ThroughDotnetHost_IncludesEntryAssembly()
    {
        List<string> command = AppRestart.BuildCommand("/usr/share/dotnet/dotnet", "/opt/conay/Conay.dll", [], 5);

        Assert.Equal(new[] { "/usr/share/dotnet/dotnet", "/opt/conay/Conay.dll", "--restarted-from", "5" }, command);
    }

    [Fact]
    public void BuildCommand_ThroughDotnetHostWithoutEntryAssembly_SkipsIt()
    {
        List<string> command = AppRestart.BuildCommand("/usr/share/dotnet/dotnet", null, [], 5);

        Assert.Equal(new[] { "/usr/share/dotnet/dotnet", "--restarted-from", "5" }, command);
    }

    [Fact]
    public void ParsePreviousInstance_ReadsProcessId()
    {
        Assert.Equal(42, AppRestart.ParsePreviousInstance(["Conay.exe", "--server", "x", "--restarted-from", "42"]));
    }

    [Fact]
    public void ParsePreviousInstance_WithoutArgument_ReturnsNull()
    {
        Assert.Null(AppRestart.ParsePreviousInstance(["Conay.exe", "--server", "x"]));
    }

    [Fact]
    public void ParsePreviousInstance_WithoutValue_ReturnsNull()
    {
        Assert.Null(AppRestart.ParsePreviousInstance(["Conay.exe", "--restarted-from"]));
    }

    [Fact]
    public void ParsePreviousInstance_WithInvalidValue_ReturnsNull()
    {
        Assert.Null(AppRestart.ParsePreviousInstance(["Conay.exe", "--restarted-from", "abc"]));
    }

    [Fact]
    public void CreateStartInfo_StartsTheCommand()
    {
        List<string> command = [@"C:\Conay\Conay.exe", "--switch-account", "76561198000000002", "--restarted-from", "5"];

        ProcessStartInfo startInfo = AppRestart.CreateStartInfo(command, StartupEnvironmentTests.Startup);

        Assert.Equal(@"C:\Conay\Conay.exe", startInfo.FileName);
        Assert.Equal(command.Skip(1), startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
    }

    // The new instance starts Steam once it has switched, which crashed with the game settings in its environment
    [Fact]
    public void CreateStartInfo_GivesTheNewInstanceTheStartupEnvironment()
    {
        Dictionary<string, string> startup = new(StartupEnvironmentTests.Startup) { ["SteamAppId"] = "440900" };

        ProcessStartInfo startInfo = AppRestart.CreateStartInfo([@"C:\Conay\Conay.exe"], startup);

        Assert.Equal(StartupEnvironmentTests.Startup.Keys.Order(StringComparer.OrdinalIgnoreCase),
            startInfo.Environment.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }
}
