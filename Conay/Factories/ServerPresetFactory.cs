using System;
using System.Collections.Generic;
using Conay.Data;
using Conay.Services;
using Conay.ViewModels.Parts;
using Microsoft.Extensions.DependencyInjection;

namespace Conay.Factories;

public class ServerPresetFactory(IServiceProvider serviceProvider)
{
    private readonly List<ServerPresetViewModel> _serverPresets = [];

    public ServerPresetViewModel Create(ServerInfo serverInfo)
    {
        ServerPresetViewModel? preset = _serverPresets.Find(x => x.File == serverInfo.File);
        if (preset != null) return preset;

        Router router = serviceProvider.GetRequiredService<Router>();
        LauncherConfig launcherConfig = serviceProvider.GetRequiredService<LauncherConfig>();

        preset = new ServerPresetViewModel(router, launcherConfig, serverInfo);
        _serverPresets.Add(preset);

        return preset;
    }
}