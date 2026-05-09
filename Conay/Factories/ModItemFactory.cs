using System.Collections.Generic;
using Conay.Data;
using Conay.Services;
using Conay.ViewModels.Parts;

namespace Conay.Factories;

public class ModItemFactory(Steam steam, ModSourceFactory modSourceFactory, LauncherConfig launcherConfig)
{
    private readonly Dictionary<string, ModItemViewModel> _modItems = [];

    public ModItemViewModel Create(string modPath, GameVersion? version = null)
    {
        if (!_modItems.TryGetValue(modPath, out ModItemViewModel? item))
        {
            item = new ModItemViewModel(steam, launcherConfig, modSourceFactory, modPath);
            _modItems[modPath] = item;
        }

        item.Version = version ?? GameVersionHelper.Current;
        return item;
    }
}