using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Factories;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class LaunchWorker(
    ILogger<LaunchWorker> logger,
    LaunchState state,
    ModList modList,
    Steam steam,
    GameConfig gameConfig,
    LauncherConfig launcherConfig,
    ModSourceFactory modSourceFactory,
    NotifyService notifyService)
{
    private bool _launching;

    public void Launch()
    {
        if (_launching) return;
        _launching = true;

        string presetName = string.IsNullOrEmpty(state.Name) ? "last played modlist" : state.Name;

        logger.LogDebug("Launching '{Preset}'..", presetName);
        notifyService.UpdateStatus(this, $"Launching {presetName}..");

        _ = LaunchSequence();
    }

    private async Task LaunchSequence()
    {
        List<string> mods = modList.GetCurrentModList();
        List<ulong> steamMods = [];
        List<string> externalMods = [];

        foreach (string mod in mods)
        {
            string[] parts = mod.Split('/');
            string modIdOrFolder = parts[0];
            if (ulong.TryParse(modIdOrFolder, out ulong modId))
            {
                steamMods.Add(modId);
            }
            else if (modIdOrFolder == "@ratajmods")
            {
                externalMods.Add(Path.GetFileNameWithoutExtension(mod));
            }
        }

        if (steamMods.Count > 0)
        {
            await steam.CheckModUpdates(steamMods.ToArray());
        }

        if (externalMods.Count > 0)
        {
            WebSync ratajmods = (WebSync)modSourceFactory.Get("ratajmods");
            await ratajmods.CheckModUpdates(externalMods.ToArray());
        }

        if (launcherConfig.Data.LaunchGame)
        {
            await RunGame();
            Environment.Exit(0);
        }
        else
        {
            notifyService.UpdateStatus(this, "All mods are updated.");

            if (launcherConfig.Data.Clipboard && state.Ip != string.Empty)
            {
                _ = Clipboard.Get()?.SetTextAsync(state.Ip);
            }

            _launching = false;
        }
    }

    private async Task RunGame()
    {
        notifyService.UpdateStatus(this, "Launching the game..");

        if (launcherConfig.Data.DirectConnect && state.Ip != string.Empty)
        {
            gameConfig.SetLastConnected(state.Ip, state.Password);
            LaunchConan("-continuesession");
        }
        else
        {
            LaunchConan();
        }

        if (launcherConfig.Data.Clipboard && state.Ip != string.Empty)
        {
            _ = Clipboard.Get()?.SetTextAsync(state.Ip);
        }

        for (int i = 20; i > 1; i--)
        {
            notifyService.UpdateStatus(this, $"Launching the game (this window will close in {i} seconds)..");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        notifyService.UpdateStatus(this, "Launching the game..");
    }

    private void LaunchConan(string? args = null)
    {
        string exePath =
            Path.GetFullPath(Path.Combine(steam.AppInstallDir, "ConanSandbox/Binaries/Win64/ConanSandbox.exe"));
        if (File.Exists(exePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to launch the game directly (falling back to Steam protocol)!");
                Protocol.Open("steam://run/440900/");
            }
        }
        else
        {
            Protocol.Open("steam://run/440900/");
        }
    }
}