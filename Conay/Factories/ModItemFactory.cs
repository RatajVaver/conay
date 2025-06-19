using System;
using System.Collections.Generic;
using Conay.Services;
using Conay.ViewModels.Parts;
using Microsoft.Extensions.DependencyInjection;

namespace Conay.Factories;

public class ModItemFactory(IServiceProvider serviceProvider)
{
    private readonly List<ModItemViewModel> _modItems = [];

    public ModItemViewModel Create(string modPath)
    {
        ModItemViewModel? preset = _modItems.Find(x => x.ModPath == modPath);
        if (preset != null) return preset;

        Steam steam = serviceProvider.GetRequiredService<Steam>();
        ModSourceFactory modSourceFactory = serviceProvider.GetRequiredService<ModSourceFactory>();
        LauncherConfig launcherConfig = serviceProvider.GetRequiredService<LauncherConfig>();

        preset = new ModItemViewModel(steam, launcherConfig, modSourceFactory, modPath);
        _modItems.Add(preset);

        return preset;
    }
}