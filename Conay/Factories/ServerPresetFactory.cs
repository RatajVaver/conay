using System.Collections.Generic;
using Conay.Data;
using Conay.Services;
using Conay.ViewModels.Parts;

namespace Conay.Factories;

public class ServerPresetFactory(Router router, LauncherConfig launcherConfig, Steam steam, GameContext gameContext)
{
    private readonly Dictionary<string, ServerPresetViewModel> _serverPresets = new();

    public ServerPresetViewModel Create(ServerInfo serverInfo)
    {
        if (_serverPresets.TryGetValue(serverInfo.File, out ServerPresetViewModel? preset))
            return preset;

        preset = new ServerPresetViewModel(router, steam, launcherConfig, gameContext, serverInfo);
        _serverPresets[serverInfo.File] = preset;

        return preset;
    }

    public void Invalidate(string fileName) => _serverPresets.Remove(fileName);

    public IEnumerable<ServerPresetViewModel> GetAll() => _serverPresets.Values;
}