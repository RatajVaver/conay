using Conay.Data;

namespace Conay.Tests.Data;

public class SteamAccountTests
{
    [Fact]
    public void DisplayName_PrefersPersonaName()
    {
        SteamAccount account = new() { SteamId = 76561198000000001, AccountName = "alice_main", PersonaName = "Alice" };

        Assert.Equal("Alice", account.DisplayName);
    }

    [Fact]
    public void DisplayName_WithoutPersonaName_UsesAccountName()
    {
        SteamAccount account = new() { SteamId = 76561198000000001, AccountName = "alice_main" };

        Assert.Equal("alice_main", account.DisplayName);
    }
}
