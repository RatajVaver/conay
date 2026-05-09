using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Factories;

namespace Conay.Services;

public class ServerList
{
    private readonly PresetSourceFactory _sourceFactory;
    private readonly LauncherConfig _launcherConfig;
    private List<ServerInfo> _servers = [];
    private Dictionary<string, ServerInfo> _serverIndex = [];
    private readonly HashSet<string> _localRemoteConflicts = [];
    private readonly TaskCompletionSource _localLoadedTcs = new();
    private readonly TaskCompletionSource _remoteLoadedTcs = new();

    public Task WhenLocalLoaded => _localLoadedTcs.Task;
    public Task WhenRemoteLoaded => _remoteLoadedTcs.Task;

    public ServerList(PresetSourceFactory sourceFactory, LauncherConfig launcherConfig)
    {
        _sourceFactory = sourceFactory;
        _launcherConfig = launcherConfig;
        _ = LoadServers();
    }

    private void AddServer(ServerInfo server)
    {
        _servers.Add(server);
        _serverIndex[server.File] = server;
    }

    private void RebuildIndex()
    {
        _serverIndex = _servers.ToDictionary(s => s.File);
    }

    private async Task LoadServers()
    {
        IPresetService localPresets = _sourceFactory.Get("local");
        List<ServerInfo> localServers = await localPresets.GetServerList();
        foreach (ServerInfo server in localServers)
            AddServer(server);

        OrderServersByHistory();
        _localLoadedTcs.SetResult();

        if (!_launcherConfig.Data.OfflineMode)
        {
            List<Task<List<ServerInfo>>> remoteTasks = new[] { "ratajmods", "github" }
                .Select(origin => _sourceFactory.Get(origin).GetServerList())
                .ToList();

            HashSet<string> localFiles = _servers
                .Where(x => x.Provider is LocalPresets)
                .Select(x => x.File)
                .ToHashSet();
            HashSet<string> allFiles = _serverIndex.Keys.ToHashSet();

            foreach (List<ServerInfo> remoteServers in await Task.WhenAll(remoteTasks))
            {
                foreach (ServerInfo server in remoteServers)
                {
                    if (localFiles.Contains(server.File))
                        _localRemoteConflicts.Add(server.File);
                    else if (allFiles.Add(server.File))
                        AddServer(server);
                }
            }

            OrderServersByHistory();
        }

        _remoteLoadedTcs.SetResult();
    }

    private void OrderServersByHistory()
    {
        if (!_launcherConfig.Data.KeepHistory) return;

        Dictionary<string, int> historyRank = _launcherConfig.Data.History
            .Select((file, i) => (file, i))
            .ToDictionary(x => x.file, x => x.i);

        _servers = _servers
            .OrderBy(x => historyRank.TryGetValue(x.File, out int rank) ? rank : int.MaxValue)
            .ToList();

        RebuildIndex();
    }

    public ServerInfo? GetServerInfo(string fileName)
    {
        _serverIndex.TryGetValue(fileName, out ServerInfo? server);
        return server;
    }

    public async Task<ServerData?> GetServerData(string fileName)
    {
        ServerInfo? server = GetServerInfo(fileName);
        if (server == null) return null;

        return await server.Provider!.FetchServerData(fileName);
    }

    public async Task RefreshLocalServers()
    {
        _servers.RemoveAll(x => x.Provider is LocalPresets);
        IPresetService localPresets = _sourceFactory.Get("local");
        List<ServerInfo> localServers = await localPresets.GetServerList();
        _servers.InsertRange(0, localServers);
        OrderServersByHistory();
        RebuildIndex();
    }

    public List<ServerInfo> GetLocalServers()
    {
        return _servers.Where(x => x.Provider is LocalPresets).ToList();
    }

    public List<ServerInfo> GetRemoteServers()
    {
        return _servers.Where(x => x.Provider is RemotePresets).ToList();
    }

    public IReadOnlyCollection<string> GetLocalRemoteConflicts() => _localRemoteConflicts;

    public List<ServerInfo> GetFavoriteServers()
    {
        return _servers.Where(x => _launcherConfig.IsServerFavorite(x.File)).ToList();
    }
}
