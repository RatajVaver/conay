using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class SteamAccounts(
    ILogger<SteamAccounts> logger,
    ISteamConnection steam,
    ISteamHost host,
    LauncherConfig launcherConfig,
    NotifyService notifyService)
{
    private bool _switching;
    private SwitchRequest? _pendingSwitch = SwitchRequest.Current;
    private ulong _switchedTo = SwitchRequest.Current?.SteamId ?? 0;

    internal Func<string?> LocateSteamRoot { get; init; } = SteamPaths.FindSteamRoot;

    internal Func<string, Task<bool>> Confirm { get; init; } = MessageBox.Confirm;

    internal Func<string, IReadOnlyList<SteamAccount>, ulong, Task<SteamAccount?>> PickAccount { get; init; } =
        MessageBox.ChooseSteamAccount;

    internal TimeSpan CurrentAccountTimeout { get; init; } = TimeSpan.FromSeconds(10);

    internal TimeSpan ModUpdateCheckInterval { get; init; } = TimeSpan.FromSeconds(1);

    // How long a switch waits for Steam to stop showing Conan Exiles as running, it takes about 2.5s after the
    // previous Conay exits
    internal TimeSpan RunningGameTimeout { get; init; } = TimeSpan.FromSeconds(15);

    internal TimeSpan RunningGameCheckInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    // The switch this instance was started to finish, normally read from the command line
    internal SwitchRequest? PendingSwitch
    {
        get => _pendingSwitch;
        init
        {
            _pendingSwitch = value;
            _switchedTo = value?.SteamId ?? 0;
        }
    }

    private IReadOnlyList<SteamAccount> _savedAccounts = [];
    private string? _savedAccountsPath;
    private DateTime _savedAccountsModified;

    public IReadOnlyList<SteamAccount> GetSavedAccounts()
    {
        string? root = LocateSteamRoot();
        if (root == null) return [];

        string path = Path.Combine(root, "config", "loginusers.vdf");
        if (!File.Exists(path)) return [];

        DateTime modified = File.GetLastWriteTimeUtc(path);
        if (path == _savedAccountsPath && modified == _savedAccountsModified)
            return _savedAccounts;

        VdfNode? users = null;
        try
        {
            users = VdfNode.Parse(File.ReadAllText(path)).GetNode("users");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read saved Steam accounts!");
        }

        List<SteamAccount> accounts = [];
        foreach ((string key, VdfNode user) in users?.Children ?? [])
        {
            string? accountName = user.GetString("AccountName");
            if (!ulong.TryParse(key, out ulong steamId) || string.IsNullOrEmpty(accountName)) continue;

            accounts.Add(new SteamAccount
            {
                SteamId = steamId,
                AccountName = accountName,
                PersonaName = user.GetString("PersonaName") ?? string.Empty,
                RememberPassword = user.GetString("RememberPassword") == "1",
                MostRecent = user.GetString("MostRecent") == "1",
                AvatarPath = Path.Combine(root, "config", "avatarcache", $"{steamId}.png")
            });
        }

        _savedAccounts = accounts.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        _savedAccountsPath = path;
        _savedAccountsModified = modified;

        return _savedAccounts;
    }

    public async Task<bool> SwitchTo(ulong steamId, string? serverFile = null)
    {
        SteamAccount? account = GetSavedAccounts().FirstOrDefault(a => a.SteamId == steamId);
        if (account != null)
            return await SwitchTo(account, serverFile);

        if (await GetCurrentSteamId() == steamId)
            return true;

        notifyService.UpdateStatus(this, $"Steam account {steamId} is not saved in Steam! Log into it in Steam once first.");
        return false;
    }

    // Returns whether this instance can carry on (with the launch), which it can't after handing over to a new one
    public async Task<bool> SwitchTo(SteamAccount account, string? serverFile = null)
    {
        if (_switching) return false;
        _switching = true;

        bool justSwitched = _switchedTo == account.SteamId;
        _switchedTo = 0;

        try
        {
            if (await GetCurrentSteamId() == account.SteamId)
            {
                if (justSwitched)
                    logger.LogInformation("Switched Steam account to {Account}", account.AccountName);

                return true;
            }

            // This instance was started right after switching, so trying again would just restart Steam in a loop
            if (justSwitched)
            {
                logger.LogWarning("Steam did not log in as {Account} after switching!", account.AccountName);
                notifyService.UpdateStatus(this,
                    $"Steam did not log in as {account.DisplayName}. Sign in with \"Remember me\" checked, then try again.");
                return false;
            }

            logger.LogInformation("Switching Steam account to {Account}..", account.AccountName);
            notifyService.UpdateStatus(this, $"Switching Steam account to {account.DisplayName}..");

            await HandOver(new SwitchRequest(account.SteamId), serverFile);
            return false;
        }
        finally
        {
            _switching = false;
        }
    }

    // Returns the chosen account, 0 to stay on the current one, or null if cancelled
    public async Task<ulong?> ChooseAccount(string serverName)
    {
        IReadOnlyList<SteamAccount> accounts = GetSavedAccounts();
        if (accounts.Count < 2) return 0;

        SteamAccount? account = await PickAccount(
            $"Which Steam account do you want to play {serverName} with?", accounts, steam.CurrentSteamId);
        return account?.SteamId;
    }

    public async Task AddAccount()
    {
        if (_switching) return;

        bool confirmed = await Confirm(
            "Steam will restart to its sign-in screen and Conay will restart too.\n\n" +
            "If Steam lists your saved accounts, choose \"Add account\". Sign in with the new account and keep " +
            "\"Remember me\" checked, so Conay can switch to it later.\n\nContinue?");
        if (!confirmed) return;

        _switching = true;

        try
        {
            logger.LogInformation("Restarting Steam to add an account..");
            notifyService.UpdateStatus(this, "Restarting Steam to add an account..");
            await HandOver(SwitchRequest.AddAccount, null);
        }
        finally
        {
            _switching = false;
        }
    }

    // Passes the switch on to a fresh Conay instance, which closes Steam once this one is gone and Steam has let go
    // of it. Reconnecting this process to the restarted Steam instead left Conan Exiles showing as running forever.
    private async Task HandOver(SwitchRequest request, string? serverFile)
    {
        if (steam.IsGameRunning)
        {
            notifyService.UpdateStatus(this, "Close Conan Exiles before switching Steam accounts!");
            return;
        }

        while (steam.IsUpdatingMods)
        {
            notifyService.UpdateStatus(this, "Waiting for mod downloads to finish..");
            await Task.Delay(ModUpdateCheckInterval);
        }

        launcherConfig.SaveConfig();
        steam.Disconnect();

        string[] args = serverFile != null ? ["--server", serverFile, .. request.ToArgs()] : request.ToArgs();
        if (host.RestartConay(args))
            return;

        steam.Resume();
        notifyService.UpdateStatus(this, "Failed to restart Conay! Restart it manually to finish.");
    }

    // Finishes the switch this instance was started for. Runs once at startup while the Steam service stays away
    // from Steam: closes Steam, prepares how it signs in next and starts it again.
    public async Task FinishPendingSwitch()
    {
        SwitchRequest? request = _pendingSwitch;
        _pendingSwitch = null;
        if (request == null || _switching) return;

        _switching = true;

        try
        {
            SteamAccount? account = request.IsAddAccount
                ? null
                : GetSavedAccounts().FirstOrDefault(a => a.SteamId == request.SteamId);

            if (!request.IsAddAccount && account == null)
            {
                notifyService.UpdateStatus(this,
                    $"Steam account {request.SteamId} is not saved in Steam! Log into it in Steam once first.");
                return;
            }

            if (steam.IsGameRunning)
            {
                notifyService.UpdateStatus(this, "Close Conan Exiles before switching Steam accounts!");
                return;
            }

            notifyService.UpdateStatus(this, account != null
                ? $"Switching Steam account to {account.DisplayName}.."
                : "Restarting Steam to add an account..");

            // Steam lets go of the previous instance a couple of seconds after it exits. Anything still connected
            // as Conan Exiles after that (like another Conay window) would freeze when Steam is closed.
            if (!await WaitUntilGameNotMarkedRunning())
            {
                notifyService.UpdateStatus(this,
                    "Steam still shows Conan Exiles as running! Close it and any other Conay window, then try again.");
                return;
            }

            if (!await host.ShutdownSteam())
            {
                notifyService.UpdateStatus(this, "Failed to close Steam! Close it manually and try again.");
                return;
            }

            host.ClearRunningApps();
            if (account != null)
                PrepareLogin(account);
            else
                PrepareNewLogin();

            steam.LaunchSteam();
        }
        finally
        {
            // The Steam service connects once Steam is up (or right away if it was never closed)
            steam.Resume();
            _switching = false;
        }
    }

    // Returns false if Steam still shows Conan Exiles as running after the timeout
    private async Task<bool> WaitUntilGameNotMarkedRunning()
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (host.IsSteamRunning && host.IsGameMarkedRunning)
        {
            if (waited.Elapsed >= RunningGameTimeout) return false;
            await Task.Delay(RunningGameCheckInterval);
        }

        return true;
    }

    private async Task<ulong> GetCurrentSteamId()
    {
        if (!host.IsSteamRunning) return 0;

        Stopwatch waited = Stopwatch.StartNew();
        while (steam.CurrentSteamId == 0 && waited.Elapsed < CurrentAccountTimeout)
            await Task.Delay(500);

        return steam.CurrentSteamId;
    }

    private void PrepareLogin(SteamAccount account)
    {
        try
        {
            UpdateLoginUsers(account.SteamId);
            host.SetAutoLogin(account.AccountName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare Steam login for {Account}!", account.AccountName);
        }
    }

    private void PrepareNewLogin()
    {
        try
        {
            // With no account to log into automatically, Steam opens its sign-in screen instead
            UpdateLoginUsers(0);
            host.SetAutoLogin(string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare Steam for a new login!");
        }
    }

    private void UpdateLoginUsers(ulong steamId)
    {
        string root = LocateSteamRoot() ?? throw new DirectoryNotFoundException("Steam folder not found");
        string path = Path.Combine(root, "config", "loginusers.vdf");
        if (!File.Exists(path)) return;

        VdfNode vdf = VdfNode.Parse(File.ReadAllText(path));
        ApplyLogin(vdf, steamId);

        File.Copy(path, path + ".bak", overwrite: true);
        File.WriteAllText(path, vdf.ToString());
    }

    // Marks the account Steam should log into next, 0 marks none so Steam opens its sign-in screen
    internal static void ApplyLogin(VdfNode loginUsers, ulong steamId)
    {
        VdfNode users = loginUsers.GetNode("users") ??
                        throw new InvalidDataException("No users found in loginusers.vdf");

        string targetId = steamId.ToString();
        foreach ((string key, VdfNode user) in users.Children)
        {
            bool isTarget = key == targetId;
            user.SetString("MostRecent", isTarget ? "1" : "0");
            if (!isTarget) continue;

            user.SetString("RememberPassword", "1");
            user.SetString("AllowAutoLogin", "1");
        }
    }
}
