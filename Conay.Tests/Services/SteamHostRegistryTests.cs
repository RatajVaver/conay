using System.Runtime.Versioning;
using Conay.Services;
using Conay.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace Conay.Tests.Services;

// Runs against a throwaway key under HKCU\Software\ConayTests, never against Steam's real one
[SupportedOSPlatform("windows")]
public class SteamHostRegistryTests : IDisposable
{
    private const string ParentKeyPath = @"Software\ConayTests";

    private readonly string _keyPath = $@"{ParentKeyPath}\{Guid.NewGuid():N}";
    private readonly RegistryKey _steamKey;

    public SteamHostRegistryTests()
    {
        _steamKey = Registry.CurrentUser.CreateSubKey(_keyPath);
    }

    public void Dispose()
    {
        _steamKey.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

        try
        {
            using RegistryKey? parent = Registry.CurrentUser.OpenSubKey(ParentKeyPath);
            if (parent is { SubKeyCount: 0 })
                Registry.CurrentUser.DeleteSubKey(ParentKeyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // ignored, another test may still be using it
        }
    }

    private SteamHost CreateHost() =>
        new(NullLogger<SteamHost>.Instance, new NotifyService()) { RegistryKeyPath = _keyPath };

    [WindowsFact]
    public void ApplyAutoLogin_SetsAccountAndRememberPassword()
    {
        SteamHost.ApplyAutoLogin(_steamKey, "alice_alt");

        Assert.Equal("alice_alt", _steamKey.GetValue("AutoLoginUser"));
        Assert.Equal(RegistryValueKind.String, _steamKey.GetValueKind("AutoLoginUser"));
        Assert.Equal(1, _steamKey.GetValue("RememberPassword"));
        Assert.Equal(RegistryValueKind.DWord, _steamKey.GetValueKind("RememberPassword"));
    }

    [WindowsFact]
    public void ApplyAutoLogin_WithEmptyName_ClearsAutomaticLogin()
    {
        SteamHost.ApplyAutoLogin(_steamKey, "alice_main");

        SteamHost.ApplyAutoLogin(_steamKey, string.Empty);

        Assert.Equal(string.Empty, _steamKey.GetValue("AutoLoginUser"));
    }

    [WindowsFact]
    public void ClearRunningApps_ResetsGamesSteamThoughtWereRunning()
    {
        _steamKey.SetValue("RunningAppID", 440900, RegistryValueKind.DWord);
        SetAppValue("440900", "Running", 1);
        SetAppValue("570", "Running", 1);
        SetAppValue("730", "Running", 0);

        SteamHost.ClearRunningApps(_steamKey);

        Assert.Equal(0, _steamKey.GetValue("RunningAppID"));
        Assert.Equal(0, GetAppValue("440900", "Running"));
        Assert.Equal(0, GetAppValue("570", "Running"));
        Assert.Equal(0, GetAppValue("730", "Running"));
    }

    [WindowsFact]
    public void ClearRunningApps_KeepsOtherAppValues()
    {
        SetAppValue("440900", "Running", 1);
        SetAppValue("440900", "Installed", 1);

        SteamHost.ClearRunningApps(_steamKey);

        Assert.Equal(1, GetAppValue("440900", "Installed"));
    }

    [WindowsFact]
    public void ClearRunningApps_DoesNotCreateMissingValues()
    {
        SetAppValue("440900", "Installed", 1);

        SteamHost.ClearRunningApps(_steamKey);

        Assert.Null(_steamKey.GetValue("RunningAppID"));
        Assert.Null(GetAppValue("440900", "Running"));
    }

    [WindowsFact]
    public void ClearRunningApps_WithoutAppsKey_StillResetsRunningAppId()
    {
        _steamKey.SetValue("RunningAppID", 440900, RegistryValueKind.DWord);

        SteamHost.ClearRunningApps(_steamKey);

        Assert.Equal(0, _steamKey.GetValue("RunningAppID"));
    }

    [WindowsFact]
    public void Host_SetAutoLogin_WritesToSteamsRegistryKey()
    {
        CreateHost().SetAutoLogin("alice_alt");

        Assert.Equal("alice_alt", _steamKey.GetValue("AutoLoginUser"));
        Assert.Equal(1, _steamKey.GetValue("RememberPassword"));
    }

    [WindowsFact]
    public void Host_ClearRunningApps_ResetsSteamsRegistryKey()
    {
        _steamKey.SetValue("RunningAppID", 440900, RegistryValueKind.DWord);
        SetAppValue("440900", "Running", 1);

        CreateHost().ClearRunningApps();

        Assert.Equal(0, _steamKey.GetValue("RunningAppID"));
        Assert.Equal(0, GetAppValue("440900", "Running"));
    }

    [WindowsFact]
    public void Host_ClearRunningApps_WithoutSteamKey_DoesNothing()
    {
        SteamHost host = new(NullLogger<SteamHost>.Instance, new NotifyService())
        {
            RegistryKeyPath = $@"{_keyPath}\Missing"
        };

        host.ClearRunningApps();

        using RegistryKey? missing = Registry.CurrentUser.OpenSubKey($@"{_keyPath}\Missing");
        Assert.Null(missing);
    }

    [WindowsFact]
    public void IsGameMarkedRunning_ReadsRunningAppId()
    {
        _steamKey.SetValue("RunningAppID", 440900, RegistryValueKind.DWord);

        Assert.True(SteamHost.IsGameMarkedRunningIn(_steamKey));
    }

    [WindowsFact]
    public void IsGameMarkedRunning_ReadsConanExilesRunningNextToAnotherGame()
    {
        _steamKey.SetValue("RunningAppID", 730, RegistryValueKind.DWord);
        SetAppValue("440900", "Running", 1);

        Assert.True(SteamHost.IsGameMarkedRunningIn(_steamKey));
    }

    [WindowsFact]
    public void IsGameMarkedRunning_WithOnlyAnotherGameRunning_IsFalse()
    {
        _steamKey.SetValue("RunningAppID", 730, RegistryValueKind.DWord);
        SetAppValue("730", "Running", 1);
        SetAppValue("440900", "Running", 0);

        Assert.False(SteamHost.IsGameMarkedRunningIn(_steamKey));
    }

    [WindowsFact]
    public void IsGameMarkedRunning_WithoutValues_IsFalse()
    {
        Assert.False(SteamHost.IsGameMarkedRunningIn(_steamKey));
    }

    [WindowsFact]
    public void Host_IsGameMarkedRunning_FollowsSteamsRegistryKey()
    {
        SteamHost host = CreateHost();
        bool before = host.IsGameMarkedRunning;

        _steamKey.SetValue("RunningAppID", 440900, RegistryValueKind.DWord);
        bool marked = host.IsGameMarkedRunning;
        host.ClearRunningApps();

        Assert.False(before);
        Assert.True(marked);
        Assert.False(host.IsGameMarkedRunning);
    }

    [WindowsFact]
    public void Host_IsGameMarkedRunning_WithoutSteamKey_IsFalse()
    {
        SteamHost host = new(NullLogger<SteamHost>.Instance, new NotifyService())
        {
            RegistryKeyPath = $@"{_keyPath}\Missing"
        };

        Assert.False(host.IsGameMarkedRunning);
    }

    private void SetAppValue(string appId, string name, int value)
    {
        using RegistryKey app = _steamKey.CreateSubKey($@"Apps\{appId}");
        app.SetValue(name, value, RegistryValueKind.DWord);
    }

    private object? GetAppValue(string appId, string name)
    {
        using RegistryKey? app = _steamKey.OpenSubKey($@"Apps\{appId}");
        return app?.GetValue(name);
    }
}
