using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;
using Conay.Views;

namespace Conay.ViewModels;

public class FavoriteViewModel : PageViewModel
{
    private readonly ServerPresetFactory _presetFactory;
    private readonly ServerList _serverList;

    public bool ListIsEmpty => Presets.Count == 0;

    public ObservableCollection<ServerPresetViewModel> Presets { get; } = [];

    public FavoriteViewModel(ServerPresetFactory presetFactory, ServerList serverList)
    {
        _presetFactory = presetFactory;
        _serverList = serverList;

        Presets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ListIsEmpty));

        _ = LoadFavoriteServers();

        WeakReferenceMessenger.Default.Send(new ScrollToTopMessage());
    }

    private async Task LoadFavoriteServers()
    {
        RefreshServers();

        while (!_serverList.LocalServersLoaded || !_serverList.RemoteServersLoaded)
        {
            await Task.Delay(50);
            RefreshServers();
        }
    }

    public void RefreshServers()
    {
        Presets.Clear();

        List<ServerInfo> servers = _serverList.GetFavoriteServers();
        foreach (ServerInfo server in servers)
        {
            Presets.Add(_presetFactory.Create(server));
        }
    }
}