using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
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
        try
        {
            if (launcherConfig.Data.BackupTotCustom)
            {
                BackupTotCustom();
            }

            launcherConfig.SaveLastLaunchedVersion(state.Version);

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

            List<string> incompatibleMods = [];

            if (steamMods.Count > 0)
            {
                if (launcherConfig.Data.AutomaticallySubscribe)
                {
                    await steam.SubscribeToMods(steamMods.ToArray());
                }

                incompatibleMods = await steam.CheckModUpdates(steamMods.ToArray(), state.Version);
            }

            if (externalMods.Count > 0)
            {
                WebSync ratajmods = (WebSync)modSourceFactory.Get("ratajmods");
                await ratajmods.CheckModUpdates(externalMods.ToArray(), state.Version);
            }

            if (incompatibleMods.Count > 0)
            {
                string versionName = GameVersionHelper.ToDisplayName(state.Version);
                string otherVersion = state.Version == GameVersion.Enhanced ? "Legacy" : "Enhanced";
                string list = string.Join("\n", incompatibleMods.ConvertAll(m => $"• {m}"));
                bool proceed = await MessageBox.Confirm(
                    $"You are trying to launch the game with mods tagged for the {otherVersion} version of the game." +
                    $"These mods will not work correctly with {versionName}:\n{list}\n\nLaunch anyway?");
                if (!proceed)
                {
                    _launching = false;
                    return;
                }
            }

            if (launcherConfig.Data.LaunchGame)
            {
                if (await RunGame())
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
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Launch sequence failed!");
            notifyService.UpdateStatus(this, "Launch failed!");
        }
        finally
        {
            _launching = false;
        }
    }

    private async Task<bool> RunGame()
    {
        notifyService.UpdateStatus(this, "Launching the game..");

        bool launched;
        if (launcherConfig.Data.DirectConnect && !string.IsNullOrEmpty(state.Ip))
        {
            gameConfig.SetLastConnected(state.Ip, state.Password, steam.GetInstallDirForVersion(state.Version));
            if (state.Version == GameVersion.Enhanced)
                WriteModRestartData(state.Ip, state.Password);
            launched = LaunchConan("-continuesession");
        }
        else
        {
            launched = LaunchConan();
        }

        if (!launched)
        {
            _launching = false;
            return false;
        }

        if (launcherConfig.Data.Clipboard && !string.IsNullOrEmpty(state.Ip))
        {
            DataTransfer clipData = new();
            clipData.Add(DataTransferItem.CreateText(state.Ip));
            _ = Clipboard.Get()?.SetDataAsync(clipData);
        }

        int countdown = OperatingSystem.IsWindows() ? (state.Version == GameVersion.Enhanced ? 10 : 20) : 3;
        for (int i = countdown; i > 1; i--)
        {
            notifyService.UpdateStatus(this, $"Launching the game (this window will close in {i} seconds)..");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        notifyService.UpdateStatus(this, "Launching the game..");
        return true;
    }

    private bool LaunchConan(string? args = null)
    {
        if (!OperatingSystem.IsWindows())
            return LaunchViaSteamUri(args);

        string installDir = steam.GetInstallDirForVersion(state.Version);
        string exe = state.BattlEye
            ? "ConanSandbox_BE.exe"
            : (state.Version == GameVersion.Enhanced ? "ConanSandbox-Win64-Shipping.exe" : "ConanSandbox.exe");
        string exePath = Path.GetFullPath(Path.Combine(installDir, $"ConanSandbox/Binaries/Win64/{exe}"));

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
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to launch the game directly (falling back to Steam protocol)!");
                if (!steam.DualInstallMode || state.Version == GameVersionHelper.Current)
                    return LaunchViaSteamUri(args);

                notifyService.UpdateStatus(this, "Failed to launch the game!");
                return false;
            }
        }

        if (!steam.DualInstallMode || state.Version == GameVersionHelper.Current)
            return LaunchViaSteamUri(args);

        string versionName = GameVersionHelper.ToDisplayName(state.Version);
        notifyService.UpdateStatus(this, $"{versionName} executable not found! Check your installation.");
        return false;
    }

    private bool LaunchViaSteamUri(string? args = null)
    {
        string uri = args is not null
            ? $"steam://run/{GameVersionHelper.AppId}//{args}/"
            : $"steam://run/{GameVersionHelper.AppId}/";

        if (OperatingSystem.IsLinux())
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "launch.sh");
            File.WriteAllText(scriptPath,
                $"#!/bin/bash\nsleep 5\n" +
                $"if command -v steam >/dev/null 2>&1; then nohup steam \"{uri}\" >/dev/null 2>&1 &\n" +
                $"else xdg-open \"{uri}\"\nfi\nrm -f \"$0\"\n");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = scriptPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to launch Steam URI via script!");
                Protocol.Open(uri);
            }

            return true;
        }

        Protocol.Open(uri);
        return true;
    }

    private void WriteModRestartData(string serverAddress, string serverPassword)
    {
        string installDir = steam.GetInstallDirForVersion(GameVersion.Enhanced);
        string path = Path.GetFullPath(Path.Combine(installDir, "ConanSandbox/Saved/ModRestartData.json"));
        string? dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        try
        {
            string json = JsonSerializer.Serialize(new { ServerAddress = serverAddress, ServerPassword = serverPassword });
            File.WriteAllText(path, json);
            logger.LogDebug("ModRestartData.json written for {Address}", serverAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write ModRestartData.json!");
        }
    }

    private void BackupTotCustom()
    {
        string installDir = steam.GetInstallDirForVersion(state.Version);
        string sourceDir = Path.GetFullPath(
            Path.Combine(installDir, "ConanSandbox/Saved/SaveGames/TotCustom"));

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