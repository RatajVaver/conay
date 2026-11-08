using Conay.Services;
using Conay.Utils;

namespace Conay.Tests.Services;

// The registry.vdf edits Steam on Linux gets (Windows uses the real registry, see SteamHostRegistryTests)
public class SteamHostTests
{
    [Fact]
    public void ApplyAutoLogin_UpdatesExistingRegistryKey()
    {
        VdfNode registry = VdfNode.Parse("\"Registry\"\n{\n\t\"HKCU\"\n\t{\n\t\t\"Software\"\n\t\t{\n" +
                                         "\t\t\t\"valve\"\n\t\t\t{\n\t\t\t\t\"steam\"\n\t\t\t\t{\n" +
                                         "\t\t\t\t\t\"language\"\t\t\"english\"\n" +
                                         "\t\t\t\t\t\"AutoLoginUser\"\t\t\"alice_main\"\n" +
                                         "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n");

        SteamHost.ApplyAutoLogin(registry, "alice_alt");

        VdfNode steamKey = SteamKey(VdfNode.Parse(registry.ToString()))!;
        Assert.Equal("alice_alt", steamKey.GetString("AutoLoginUser"));
        Assert.Equal("1", steamKey.GetString("RememberPassword"));
        Assert.Equal("english", steamKey.GetString("language"));
        Assert.Single(registry.Children);
    }

    [Fact]
    public void ApplyAutoLogin_CreatesMissingRegistryKeys()
    {
        VdfNode registry = VdfNode.Parse(string.Empty);

        SteamHost.ApplyAutoLogin(registry, "alice_alt");

        Assert.Equal("alice_alt", SteamKey(registry)!.GetString("AutoLoginUser"));
    }

    [Fact]
    public void ApplyAutoLogin_WithEmptyName_ClearsAutomaticLogin()
    {
        VdfNode registry = VdfNode.Parse(string.Empty);
        SteamHost.ApplyAutoLogin(registry, "alice_main");

        SteamHost.ApplyAutoLogin(registry, string.Empty);

        Assert.Equal(string.Empty, SteamKey(registry)!.GetString("AutoLoginUser"));
    }

    [Fact]
    public void ClearRunningApps_ResetsGamesSteamThoughtWereRunning()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry(
            "\t\t\t\t\t\"RunningAppID\"\t\t\"440900\"\n" +
            "\t\t\t\t\t\"Apps\"\n\t\t\t\t\t{\n" +
            "\t\t\t\t\t\t\"440900\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Running\"\t\t\"1\"\n" +
            "\t\t\t\t\t\t\t\"Installed\"\t\t\"1\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t\t\"730\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Running\"\t\t\"0\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t}\n"));

        bool changed = SteamHost.ClearRunningApps(registry);

        VdfNode steamKey = SteamKey(registry)!;
        Assert.True(changed);
        Assert.Equal("0", steamKey.GetString("RunningAppID"));
        Assert.Equal("0", steamKey.GetNode("Apps")!.GetNode("440900")!.GetString("Running"));
        Assert.Equal("1", steamKey.GetNode("Apps")!.GetNode("440900")!.GetString("Installed"));
        Assert.Equal("0", steamKey.GetNode("Apps")!.GetNode("730")!.GetString("Running"));
    }

    [Fact]
    public void ClearRunningApps_WithNothingRunning_ReportsNoChange()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry(
            "\t\t\t\t\t\"RunningAppID\"\t\t\"0\"\n" +
            "\t\t\t\t\t\"Apps\"\n\t\t\t\t\t{\n" +
            "\t\t\t\t\t\t\"440900\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Installed\"\t\t\"1\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t}\n"));

        Assert.False(SteamHost.ClearRunningApps(registry));
        Assert.Null(SteamKey(registry)!.GetNode("Apps")!.GetNode("440900")!.GetString("Running"));
    }

    [Fact]
    public void ClearRunningApps_WithoutSteamKey_ReportsNoChange()
    {
        Assert.False(SteamHost.ClearRunningApps(VdfNode.Parse("\"Registry\"\n{\n}\n")));
    }

    [Fact]
    public void ClearRunningApps_WithoutApps_StillResetsRunningAppId()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry("\t\t\t\t\t\"RunningAppID\"\t\t\"440900\"\n"));

        Assert.True(SteamHost.ClearRunningApps(registry));
        Assert.Equal("0", SteamKey(registry)!.GetString("RunningAppID"));
    }

    [Fact]
    public void IsGameMarkedRunning_WhenConanExilesIsTheRunningApp()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry("\t\t\t\t\t\"RunningAppID\"\t\t\"440900\"\n"));

        Assert.True(SteamHost.IsGameMarkedRunningIn(registry));
    }

    [Fact]
    public void IsGameMarkedRunning_WhenConanExilesRunsNextToAnotherGame()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry(
            "\t\t\t\t\t\"RunningAppID\"\t\t\"730\"\n" +
            "\t\t\t\t\t\"Apps\"\n\t\t\t\t\t{\n" +
            "\t\t\t\t\t\t\"440900\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Running\"\t\t\"1\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t}\n"));

        Assert.True(SteamHost.IsGameMarkedRunningIn(registry));
    }

    [Fact]
    public void IsGameMarkedRunning_WithOnlyAnotherGameRunning_IsFalse()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry(
            "\t\t\t\t\t\"RunningAppID\"\t\t\"730\"\n" +
            "\t\t\t\t\t\"Apps\"\n\t\t\t\t\t{\n" +
            "\t\t\t\t\t\t\"440900\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Running\"\t\t\"0\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t\t\"730\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Running\"\t\t\"1\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t}\n"));

        Assert.False(SteamHost.IsGameMarkedRunningIn(registry));
    }

    [Fact]
    public void IsGameMarkedRunning_WithNothingRunning_IsFalse()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry("\t\t\t\t\t\"RunningAppID\"\t\t\"0\"\n"));

        Assert.False(SteamHost.IsGameMarkedRunningIn(registry));
    }

    [Fact]
    public void IsGameMarkedRunning_WithoutSteamKey_IsFalse()
    {
        Assert.False(SteamHost.IsGameMarkedRunningIn(VdfNode.Parse("\"Registry\"\n{\n}\n")));
    }

    [Fact]
    public void IsGameMarkedRunning_AfterClearRunningApps_IsFalse()
    {
        VdfNode registry = VdfNode.Parse(SteamRegistry(
            "\t\t\t\t\t\"RunningAppID\"\t\t\"440900\"\n" +
            "\t\t\t\t\t\"Apps\"\n\t\t\t\t\t{\n" +
            "\t\t\t\t\t\t\"440900\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"Running\"\t\t\"1\"\n\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t}\n"));

        SteamHost.ClearRunningApps(registry);

        Assert.False(SteamHost.IsGameMarkedRunningIn(registry));
    }

    private static string SteamRegistry(string steamValues) =>
        "\"Registry\"\n{\n\t\"HKCU\"\n\t{\n\t\t\"Software\"\n\t\t{\n\t\t\t\"Valve\"\n\t\t\t{\n" +
        "\t\t\t\t\"Steam\"\n\t\t\t\t{\n" + steamValues + "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    private static VdfNode? SteamKey(VdfNode registry) =>
        registry.GetNode("Registry")?.GetNode("HKCU")?.GetNode("Software")?.GetNode("Valve")?.GetNode("Steam");
}
