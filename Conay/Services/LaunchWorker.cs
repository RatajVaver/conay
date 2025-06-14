using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Factories;
using Conay.Utils;

namespace Conay.Services;

public class LaunchWorker(
    LaunchState state,
    ModList modList,
    Steam steam,
    ServerList serverList,
    GameConfig gameConfig,
    LauncherConfig launcherConfig,
    ModSourceFactory modSourceFactory)
{
    public event EventHandler<string>? StatusChanged;
    private bool _launching;

    public void Launch()
    {
        if (_launching) return;
        _launching = true;

        string presetName = string.IsNullOrEmpty(state.Name) ? "last played modlist" : state.Name;

        Console.WriteLine($"Launching '{presetName}'..");
        StatusChanged?.Invoke(this, $"Launching {presetName}..");

        _ = LaunchSequence();
    }

    public async Task PrepareDefault()
    {
        string lastServer = launcherConfig.Data.History.FirstOrDefault(string.Empty);
        if (lastServer == string.Empty) return;

        while (!serverList.LocalServersLoaded || !serverList.RemoteServersLoaded)
        {
            await Task.Delay(200);
        }

        ServerInfo? serverInfo = serverList.GetServerInfo(lastServer);
        if (serverInfo == null) return;

        ServerData? serverData = await serverList.GetServerData(lastServer);
        if (serverData == null) return;

        state.Name = serverData.Name;
        state.Ip = serverData.Ip;
        modList.SaveModList(serverData.Mods.ToArray());
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

        await steam.CheckModUpdates(steamMods.ToArray());

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
            StatusChanged?.Invoke(this, "All mods are updated.");

            if (launcherConfig.Data.Clipboard)
            {
                _ = Clipboard.Get().SetTextAsync(state.Ip);
            }

            _launching = false;
        }
    }

    private async Task RunGame()
    {
        StatusChanged?.Invoke(this, "Launching the game..");

        if (launcherConfig.Data.DirectConnect)
        {
            gameConfig.SetLastConnected(state.Ip, state.Password);
            LaunchConan("-continuesession");
        }
        else
        {
            LaunchConan();
        }

        if (launcherConfig.Data.Clipboard)
        {
            _ = Clipboard.Get().SetTextAsync(state.Ip);
        }

        for (int i = 20; i > 1; i--)
        {
            StatusChanged?.Invoke(this, $"Launching the game (this window will close in {i} seconds)..");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        StatusChanged?.Invoke(this, "Launching the game..");
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
                Console.WriteLine(ex);
                Protocol.Open("steam://run/440900/");
            }
        }
        else
        {
            Protocol.Open("steam://run/440900/");
        }
    }
}