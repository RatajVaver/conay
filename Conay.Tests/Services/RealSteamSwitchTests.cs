using System.Diagnostics;
using System.Runtime.Versioning;
using Conay.Data;
using Conay.Services;
using Conay.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conay.Tests.Services;

// Really switches Steam accounts on this machine with Conay's own switching code: closes Steam, logs into another
// saved account, then switches back. Opt in by setting CONAY_REAL_STEAM=1. It needs Steam running and
// logged in, a second account saved with "Remember me", and Conan Exiles closed.
[Collection(RealSteamCollection.Name)]
[SupportedOSPlatform("windows")]
public class RealSteamSwitchTests : IDisposable
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(3);

    private readonly TempDirectory _dir = new();
    private readonly List<string> _statuses = [];

    public void Dispose() => _dir.Dispose();

    [RealSteamFact]
    public async Task SwitchesToAnotherSavedAccountAndBack()
    {
        ulong original = RegistrySteamConnection.ActiveSteamId();
        Assert.True(Steam.IsSteamRunning() && original != 0, "Start Steam and log in before running this test");

        IReadOnlyList<SteamAccount> saved = CreateAccounts(new LocalHandOver(CreateHost())).GetSavedAccounts();
        SteamAccount? originalAccount = saved.FirstOrDefault(a => a.SteamId == original);
        SteamAccount? otherAccount = saved.FirstOrDefault(a => a.SteamId != original && a.RememberPassword);
        Assert.True(originalAccount != null, "The logged in account is not saved in Steam");
        Assert.True(otherAccount != null, "Save a second account in Steam with \"Remember me\" to run this test");

        try
        {
            await SwitchAndWaitForLogin(otherAccount);
        }
        finally
        {
            await SwitchAndWaitForLogin(originalAccount);
        }
    }

    private SteamAccounts CreateAccounts(ISteamHost host, SwitchRequest? pendingSwitch = null)
    {
        NotifyService notifyService = new();
        notifyService.StatusChanged += (_, status) => _statuses.Add(status);

        return new SteamAccounts(NullLogger<SteamAccounts>.Instance, new RegistrySteamConnection(), host,
            TestServices.CreateConfig(_dir), notifyService)
        {
            PendingSwitch = pendingSwitch
        };
    }

    private static SteamHost CreateHost() => new(NullLogger<SteamHost>.Instance, new NotifyService());

    private async Task SwitchAndWaitForLogin(SteamAccount account)
    {
        LocalHandOver host = new(CreateHost());

        Assert.False(await CreateAccounts(host).SwitchTo(account));
        string[] args = Assert.IsType<string[]>(host.HandedOverArgs);

        // What the new Conay instance does once it has started
        await CreateAccounts(host, SwitchRequest.FromArgs(args)).FinishPendingSwitch();

        Stopwatch waited = Stopwatch.StartNew();
        while (waited.Elapsed < LoginTimeout)
        {
            if (Steam.IsSteamRunning() && RegistrySteamConnection.ActiveSteamId() == account.SteamId)
            {
                // Give Steam a moment before it's closed again
                await Task.Delay(TimeSpan.FromSeconds(5));
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail($"Steam did not log in as {account.DisplayName} within {LoginTimeout.TotalMinutes} minutes " +
                    $"({string.Join(" / ", _statuses)})");
    }
}
