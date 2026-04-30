using System;
using Conay.Data;

namespace Conay.Services;

public class Router(LaunchState launchState, LauncherConfig launcherConfig)
{
    public event Action<string?>? OnBeforeLaunch;
    public event Action<ServerData?>? ShowLaunchForPreset;
    public event Action? OnShowAddPreset;
    public event Action<ServerData>? OnShowEditPreset;
    public event Action? OnShowPresets;

    public void BeforeLaunch(string? name = null)
    {
        OnBeforeLaunch?.Invoke(name);
    }

    public void ShowAddPreset() => OnShowAddPreset?.Invoke();
    public void ShowEditPreset(ServerData preset) => OnShowEditPreset?.Invoke(preset);
    public void ShowPresets() => OnShowPresets?.Invoke();

    public void ReadyForLaunch(ServerData? preset, bool isSaveLaunch = false)
    {
        if (preset?.FileName != null)
        {
            launcherConfig.SaveIntoHistory(preset.FileName);
        }

        launchState.Name = preset?.Name ?? string.Empty;
        launchState.Ip = preset?.Ip ?? string.Empty;
        launchState.IsSaveLaunch = isSaveLaunch;
        launchState.BattlEye = preset?.BattlEye ?? false;
        ShowLaunchForPreset?.Invoke(preset);
    }
}