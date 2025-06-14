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
    public bool LocalServersLoaded;
    public bool RemoteServersLoaded;

    public ServerList(PresetSourceFactory sourceFactory, LauncherConfig launcherConfig)
    {
        _sourceFactory = sourceFactory;
        _launcherConfig = launcherConfig;
        _ = LoadServers();
    }

    private async Task LoadServers()
    {
        IPresetService localPresets = _sourceFactory.Get("local");
        List<ServerInfo> localServers = await localPresets.GetServerList();
        _servers.AddRange(localServers);
        OrderServersByHistory();
        LocalServersLoaded = true;

        if (!_launcherConfig.Data.OfflineMode)
        {
            foreach (string origin in new[] { "ratajmods", "github" })
            {
                IPresetService provider = _sourceFactory.Get(origin);
                List<ServerInfo> remoteServers = await provider.GetServerList();
                foreach (ServerInfo server in remoteServers.Where(server => _servers.All(x => x.File != server.File)))
                {
                    _servers.Add(server);
                }
            }

            OrderServersByHistory();
        }

        RemoteServersLoaded = true;
    }

    private void OrderServersByHistory()
    {
        if (!_launcherConfig.Data.KeepHistory) return;

        _servers = _servers.OrderBy(x =>
        {
            int index = _launcherConfig.Data.History.IndexOf(x.File);
            return index == -1 ? int.MaxValue : index;
        }).ToList();
    }

    public ServerInfo? GetServerInfo(string fileName)
    {
        return _servers.FirstOrDefault(x => x.File == fileName);
    }

    public async Task<ServerData?> GetServerData(string fileName)
    {
        ServerInfo? server = GetServerInfo(fileName);
        if (server == null) return null;

        return await server.Provider!.FetchServerData(fileName);
    }

    public List<ServerInfo> GetLocalServers()
    {
        return _servers.Where(x => x.Provider?.GetType() == typeof(LocalPresets)).ToList();
    }

    public List<ServerInfo> GetRemoteServers()
    {
        return _servers.Where(x => x.Provider?.GetType() == typeof(RemotePresets)).ToList();
    }

    public List<ServerInfo> GetFavoriteServers()
    {
        return _servers.Where(x => _launcherConfig.IsServerFavorite(x.File)).ToList();
    }
}