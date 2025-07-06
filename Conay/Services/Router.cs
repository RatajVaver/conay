using System;
using Conay.Data;

namespace Conay.Services;

public class Router(LaunchState launchState, LauncherConfig launcherConfig)
{
    public event Action<string?>? OnBeforeLaunch;
    public event Action<ServerData?>? ShowLaunchForPreset;

    public void BeforeLaunch(string? name = null)
    {
        OnBeforeLaunch?.Invoke(name);
    }

    public void ReadyForLaunch(ServerData? preset)
    {
        if (preset?.FileName != null)
        {
            launcherConfig.SaveIntoHistory(preset.FileName);
        }

        launchState.Name = preset?.Name ?? string.Empty;
        launchState.Ip = preset?.Ip ?? string.Empty;
        ShowLaunchForPreset?.Invoke(preset);
    }
}