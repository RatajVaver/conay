using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using Conay.Data;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class SelfUpdater(ILogger<SelfUpdater> logger, HttpService http, NotifyService notifyService)
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/RatajVaver/conay/releases/latest";

    private const string InstallerDownloadUrl =
        "https://github.com/RatajVaver/conay/releases/latest/download/ConayInstaller.exe";

    private const string LinuxArchiveDownloadUrl =
        "https://github.com/RatajVaver/conay/releases/latest/download/conay-linux.tar.gz";

    private static int[] ParseVersion(string version)
    {
        int index = version.IndexOf('-');
        if (index >= 0)
            version = version[..index];

        return version.Split('.').Select(part => int.TryParse(part, out int num) ? num : 0).ToArray();
    }

    private static bool IsVersionNewer(string currentVersion, string newestVersion)
    {
        int[] localVersion = ParseVersion(currentVersion);
        int[] remoteVersion = ParseVersion(newestVersion);

        for (int i = 0; i < remoteVersion.Length; i++)
        {
            int localPart = localVersion.Length > i ? localVersion[i] : 0;

            if (remoteVersion[i] > localPart)
            {
                return true;
            }

            if (remoteVersion[i] < localPart)
            {
                return false;
            }
        }

        return false;
    }

    public async Task<bool> CheckUpdate()
    {
        string currentVersion = Meta.GetVersion();
        string json = await http.Get(LatestReleaseUrl);

        if (json == string.Empty)
        {
            logger.LogError("Failed to check Conay updates!");
            return false;
        }

        AppReleaseData? releaseData = JsonSerializer.Deserialize<AppReleaseData>(json);
        if (string.IsNullOrEmpty(releaseData?.Version))
            return false;

        string newestVersion = releaseData.Version;
        bool isPreRelease = currentVersion.Contains('-');
        return newestVersion != currentVersion && (IsVersionNewer(currentVersion, newestVersion) || (isPreRelease && !IsVersionNewer(newestVersion, currentVersion)));
    }

    private void ReportProgress(float progress)
    {
        notifyService.UpdateProgress(this, progress * 100);
    }

    public async Task DownloadInstaller()
    {
        if (OperatingSystem.IsLinux())
            await DownloadLinuxArchive();
        else
            await DownloadWindowsInstaller();
    }

    private async Task DownloadWindowsInstaller()
    {
        notifyService.UpdateStatus(this, "Downloading Conay update..");

        string appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string parentDirectory = Directory.GetParent(appDirectory)!.FullName;
        string installerPath = Path.GetFullPath(Path.Combine(appDirectory, "ConayInstaller.exe"));

        bool success =
            await http.Download(InstallerDownloadUrl, installerPath, new Progress<float>(ReportProgress));
        if (success)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/D=" + parentDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run the update installer!");
                notifyService.UpdateStatus(this, "Failed to run the update installer!");
                return;
            }

            notifyService.UpdateStatus(this,
                "Update downloaded, installer will be launched and this window will close..");

            await Task.Delay(TimeSpan.FromSeconds(3));
            Environment.Exit(0);
        }
        else
        {
            logger.LogError("Failed to download Conay update!");
            notifyService.UpdateStatus(this, "Failed to update Conay!");
        }
    }

    [SupportedOSPlatform("linux")]
    private async Task DownloadLinuxArchive()
    {
        notifyService.UpdateStatus(this, "Downloading Conay update..");

        string appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string archivePath = Path.Combine(Path.GetTempPath(), "conay-linux.tar.gz");
        string scriptPath = Path.Combine(Path.GetTempPath(), "conay-update.sh");

        bool success = await http.Download(LinuxArchiveDownloadUrl, archivePath, new Progress<float>(ReportProgress));
        if (!success)
        {
            logger.LogError("Failed to download Conay update!");
            notifyService.UpdateStatus(this, "Failed to update Conay!");
            return;
        }

        await File.WriteAllTextAsync(scriptPath,
            $"""
            #!/bin/bash
            sleep 5
            tar -xzf "{archivePath}" -C "{appDirectory}"
            rm "{archivePath}"
            rm -- "$0"
            """);

        File.SetUnixFileMode(scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run update script!");
            notifyService.UpdateStatus(this, "Failed to run update script!");
            return;
        }

        notifyService.UpdateStatus(this, "Update downloaded, applying after close..");
        await Task.Delay(TimeSpan.FromSeconds(3));
        Environment.Exit(0);
    }
}