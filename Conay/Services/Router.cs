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
            if (launcherConfig.Data.KeepHistory)
            {
                launcherConfig.Data.History.Remove(preset.FileName);
                launcherConfig.Data.History.Insert(0, preset.FileName);
            }
            else
            {
                launcherConfig.Data.History.Clear();
                launcherConfig.Data.History.Add(preset.FileName);
            }

            _ = launcherConfig.ScheduleConfigSave();
        }

        launchState.Name = preset?.Name ?? string.Empty;
        launchState.Ip = preset?.Ip ?? string.Empty;
        mvm.ShowLaunchForPreset(preset);
    }
}