using System.Collections.Generic;
using Conay.Services;
using Conay.ViewModels.Parts;

namespace Conay.Factories;

public class ModItemFactory(Steam steam, LauncherConfig launcherConfig, ModSourceFactory modSourceFactory)
{
    private readonly List<ModItemViewModel> _modItems = [];

    public ModItemViewModel Create(string modPath)
    {
        ModItemViewModel? preset = _modItems.Find(x => x.ModPath == modPath);
        if (preset != null) return preset;

        preset = new ModItemViewModel(steam, launcherConfig, modSourceFactory, modPath);
        _modItems.Add(preset);

        return preset;
    }
}