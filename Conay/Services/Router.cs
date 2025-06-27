using Conay.Data;
using Conay.ViewModels;

namespace Conay.Services;

public class Router(MainViewModel mvm, LaunchState launchState, LauncherConfig launcherConfig)
{
    public void BeforeLaunch(string? name = null)
    {
        mvm.BeforeLaunch(name);
    }

    public void ReadyForLaunch(ServerData? preset)
    {
        if (preset?.FileName != null)
        {
            launcherConfig.SaveIntoHistory(preset.FileName);
        }

        launchState.Name = preset?.Name ?? string.Empty;
        launchState.Ip = preset?.Ip ?? string.Empty;
        mvm.ShowLaunchForPreset(preset);
    }
}