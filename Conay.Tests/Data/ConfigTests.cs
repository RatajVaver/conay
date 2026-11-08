using System.Text.Json;
using Conay.Data;

namespace Conay.Tests.Data;

public class ConfigTests
{
    // Like any SteamID64, too big for a double to hold exactly
    private const ulong Alt = 76561198000000002;

    [Fact]
    public void Serialize_UsesConfigFileNames()
    {
        Config config = new();
        config.ServerAccounts["arcanasgrasp"] = Alt;
        config.AskAccountServers.Add("roguepleasures");

        string json = JsonSerializer.Serialize(config);

        Assert.Contains($"\"serverAccounts\":{{\"arcanasgrasp\":{Alt}}}", json);
        Assert.Contains("\"askAccountServers\":[\"roguepleasures\"]", json);
    }

    [Fact]
    public void Deserialize_KeepsSteamIdsExact()
    {
        Config config = JsonSerializer.Deserialize<Config>($"{{\"serverAccounts\":{{\"arcanasgrasp\":{Alt}}}}}")!;

        Assert.Equal(Alt, config.ServerAccounts["arcanasgrasp"]);
    }

    [Fact]
    public void Deserialize_ConfigFromOlderVersion_HasNoAccounts()
    {
        Config config = JsonSerializer.Deserialize<Config>("{\"checkUpdates\":true,\"favorites\":[\"arcanasgrasp\"]}")!;

        Assert.Empty(config.ServerAccounts);
        Assert.Empty(config.AskAccountServers);
    }
}
