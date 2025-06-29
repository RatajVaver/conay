using System;
using System.Collections.Generic;
using Conay.Services;
using Microsoft.Extensions.Logging;

namespace Conay.Factories;

public class ModSourceFactory
{
    private readonly Dictionary<string, IModSource> _sources;

    public ModSourceFactory(ILogger<WebSync> logger, Steam steam, ModList modList, HttpService http,
        NotifyService notifyService)
    {
        WebSync ratajmods = new(logger, http, modList, notifyService, "ratajmods",
            "https://ratajmods.net/conay/mods.json",
            "https://ratajmods.net/assets/mods");

        _sources = new Dictionary<string, IModSource>
        {
            ["steam"] = steam,
            ["ratajmods"] = ratajmods
        };
    }

    public bool IsKnownSource(string name)
    {
        return _sources.ContainsKey(name);
    }

    public IModSource Get(string name)
    {
        return _sources.TryGetValue(name, out IModSource? instance)
            ? instance
            : throw new ArgumentOutOfRangeException(nameof(name));
    }
}