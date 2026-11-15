using Conay.Data;
using Conay.Services;

namespace Conay.Tests.TestSupport;

internal sealed class FakePresetService(string providerName = "ratajmods") : IPresetService
{
    public List<string> SavedModlists { get; } = [];

    public string GetProviderName() => providerName;

    public Task<List<ServerInfo>> GetServerList() => Task.FromResult(new List<ServerInfo>());

    public Task<List<ServerInfo>> GetUnlistedServers(List<string> names) => Task.FromResult(new List<ServerInfo>());

    public Task<ServerData?> FetchServerData(string fileName) => Task.FromResult<ServerData?>(new ServerData
    {
        Name = "Arcana's Grasp RP",
        Ip = "127.0.0.1:7777",
        FileName = fileName
    });

    public void SaveModlistFromPreset(string fileName) => SavedModlists.Add(fileName);

    public event Action<List<ServerInfo>>? ServerListUpdated
    {
        add { }
        remove { }
    }
}
