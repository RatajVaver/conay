using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        AppReleaseData? releaseData = JsonSerializer.Deserialize<AppReleaseData>(json);
        if (releaseData == null)
            return false;

        string newestVersion = releaseData.Version;
        return newestVersion != currentVersion && IsVersionNewer(currentVersion, newestVersion);
    }

    private void ReportProgress(float progress)
    {
        notifyService.UpdateProgress(this, progress * 100);
    }

    public async Task DownloadInstaller()
    {
        notifyService.UpdateStatus(this, "Downloading Conay update..");

        string appDirectory = AppContext.BaseDirectory;
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
}