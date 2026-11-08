using Conay.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conay.Tests.TestSupport;

internal static class TestServices
{
    public static LauncherConfig CreateConfig(TempDirectory directory) =>
        new(NullLogger<LauncherConfig>.Instance, new NotifyService(), directory.PathOf("config.json"));

    // Steam is left out unless a test needs it, the real service would connect to (or start) the real Steam
    public static SteamAccounts CreateSteamAccounts(LauncherConfig config, string? steamRoot,
        ISteamConnection? steam = null, ISteamHost? host = null) =>
        new(NullLogger<SteamAccounts>.Instance, steam!, host!, config, new NotifyService())
        {
            LocateSteamRoot = () => steamRoot,
            CurrentAccountTimeout = TimeSpan.Zero
        };
}
