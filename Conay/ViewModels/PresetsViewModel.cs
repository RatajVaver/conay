using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;

namespace Conay.ViewModels;

public class PresetsViewModel : PageViewModel
{
    private readonly ServerPresetFactory _presetFactory;
    private readonly ServerList _serverList;

    public ObservableCollection<ServerPresetViewModel> Presets { get; } = [];

    public PresetsViewModel(ServerPresetFactory presetFactory, ServerList serverList)
    {
        _presetFactory = presetFactory;
        _serverList = serverList;

        _ = LoadLocalServers();
    }

    private async Task LoadLocalServers()
    {
        RefreshServers();

        while (!_serverList.LocalServersLoaded)
        {
            await Task.Delay(200);
            RefreshServers();
        }
    }

    private void RefreshServers()
    {
        Presets.Clear();

        List<ServerInfo> servers = _serverList.GetLocalServers();
        foreach (ServerInfo server in servers)
        {
            Presets.Add(_presetFactory.Create(server));
        }
    }
}