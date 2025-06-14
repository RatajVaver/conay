using System;
using System.Collections.Generic;
using Conay.Services;

namespace Conay.Factories;

public class ModSourceFactory
{
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    private readonly Dictionary<string, IModSource> _sources;

    public ModSourceFactory(Steam steam, ModList modList)
    {
        WebSync ratajmods = new(modList, "ratajmods",
            "https://ratajmods.net/conay/mods.json",
            "https://ratajmods.net/assets/mods");

        ratajmods.StatusChanged += (sender, status) => StatusChanged?.Invoke(sender, status);
        ratajmods.DownloadProgressChanged += (sender, progress) => DownloadProgressChanged?.Invoke(sender, progress);

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