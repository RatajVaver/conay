using System;
using System.Collections.Generic;
using Conay.Services;
using Microsoft.Extensions.Logging;

namespace Conay.Factories;

public class PresetSourceFactory(
    ILogger<RemotePresets> logger,
    LauncherConfig config,
    HttpService http,
    ModList modList,
    LocalPresets localPresets)
{
    private readonly Dictionary<string, IPresetService> _sources = new()
    {
        ["local"] = localPresets,
        ["github"] = new RemotePresets(logger, config, http, modList, "github",
            "https://raw.githubusercontent.com/RatajVaver/conay/main/servers.json",
            "https://raw.githubusercontent.com/RatajVaver/conay/main/servers"),
        ["ratajmods"] = new RemotePresets(logger, config, http, modList, "ratajmods",
            "https://ratajmods.net/conay/servers.json",
            "https://ratajmods.net/conay/servers")
    };

    public IPresetService Get(string name)
    {
        return _sources.TryGetValue(name, out IPresetService? instance)
            ? instance
            : throw new ArgumentOutOfRangeException(nameof(name));
    }
}