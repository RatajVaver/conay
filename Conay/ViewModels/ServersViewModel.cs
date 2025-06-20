using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;

namespace Conay.ViewModels;

public class ServersViewModel : PageViewModel
{
    private readonly ServerPresetFactory _presetFactory;
    private readonly ServerList _serverList;

    public bool ListIsEmpty => Presets.Count == 0;

    public ObservableCollection<ServerPresetViewModel> Presets { get; } = [];

    public ServersViewModel(ServerPresetFactory presetFactory, ServerList serverList)
    {
        _presetFactory = presetFactory;
        _serverList = serverList;

        Presets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ListIsEmpty));

        _ = LoadRemoteServers();
    }

    private async Task LoadRemoteServers()
    {
        RefreshServers();

        while (!_serverList.RemoteServersLoaded)
        {
            await Task.Delay(200);
            RefreshServers();
        }
    }

    private void RefreshServers()
    {
        Presets.Clear();

        List<ServerInfo> servers = _serverList.GetRemoteServers();
        foreach (ServerInfo server in servers)
        {
            Presets.Add(_presetFactory.Create(server));
        }
    }
}