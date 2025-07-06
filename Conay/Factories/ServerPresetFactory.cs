using System.Collections.Generic;
using Conay.Data;
using Conay.Services;
using Conay.ViewModels.Parts;

namespace Conay.Factories;

public class ServerPresetFactory(Router router, LauncherConfig launcherConfig, Steam steam)
{
    private readonly List<ServerPresetViewModel> _serverPresets = [];

    public ServerPresetViewModel Create(ServerInfo serverInfo)
    {
        ServerPresetViewModel? preset = _serverPresets.Find(x => x.File == serverInfo.File);
        if (preset != null) return preset;

        preset = new ServerPresetViewModel(router, steam, launcherConfig, serverInfo);
        _serverPresets.Add(preset);

        return preset;
    }

    public List<ServerPresetViewModel> GetAll() => _serverPresets;
}