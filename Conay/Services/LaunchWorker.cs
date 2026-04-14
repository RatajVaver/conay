using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Factories;
using Conay.Utils;
using Avalonia.Input;
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
        if (launcherConfig.Data.BackupTotCustom)
        {
            BackupTotCustom();
        }

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
            if (launcherConfig.Data.AutomaticallySubscribe)
            {
                await steam.SubscribeToMods(steamMods.ToArray());
            }

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

            if (launcherConfig.Data.Clipboard && !string.IsNullOrEmpty(state.Ip))
            {
                DataTransfer clipData = new();
                clipData.Add(DataTransferItem.CreateText(state.Ip));
                _ = Clipboard.Get()?.SetDataAsync(clipData);
            }

            _launching = false;
        }
    }

    private async Task RunGame()
    {
        notifyService.UpdateStatus(this, "Launching the game..");

        if (launcherConfig.Data.DirectConnect && !string.IsNullOrEmpty(state.Ip))
        {
            gameConfig.SetLastConnected(state.Ip, state.Password);
            LaunchConan("-continuesession");
        }
        else
        {
            LaunchConan();
        }

        if (launcherConfig.Data.Clipboard && !string.IsNullOrEmpty(state.Ip))
        {
            DataTransfer clipData = new();
            clipData.Add(DataTransferItem.CreateText(state.Ip));
            _ = Clipboard.Get()?.SetDataAsync(clipData);
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
        string exe = state.BattlEye ? "ConanSandbox_BE.exe" : "ConanSandbox.exe";
        string exePath = Path.GetFullPath(Path.Combine(steam.AppInstallDir, $"ConanSandbox/Binaries/Win64/{exe}"));
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

    private void BackupTotCustom()
    {
        string sourceDir = Path.GetFullPath(
            Path.Combine(steam.AppInstallDir, "ConanSandbox/Saved/SaveGames/TotCustom"));

        if (!Directory.Exists(sourceDir))
        {
            logger.LogDebug("TotCustom folder not found, skipping backup.");
            return;
        }

        try
        {
            string backupDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "backups"));
            Directory.CreateDirectory(backupDir);

            string slot1 = Path.Combine(backupDir, "TotCustom_1.zip");
            string slot2 = Path.Combine(backupDir, "TotCustom_2.zip");
            string slot3 = Path.Combine(backupDir, "TotCustom_3.zip");

            if (File.Exists(slot2)) File.Move(slot2, slot3, overwrite: true);
            if (File.Exists(slot1)) File.Move(slot1, slot2, overwrite: true);

            ZipFile.CreateFromDirectory(sourceDir, slot1);

            logger.LogDebug("TotCustom backup saved to '{Path}'.", slot1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to back up TotCustom!");
        }
    }
}