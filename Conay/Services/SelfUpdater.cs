using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
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
        return newestVersion != currentVersion && (IsVersionNewer(currentVersion, newestVersion) ||
                                                   (isPreRelease && !IsVersionNewer(newestVersion, currentVersion)));
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

    private const string LinuxExecutableName = "Conay";

    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const string LinuxUpdateScript =
        """
        pid="$1"; app="$2"; stage="$3"; exe="$4"

        for _ in $(seq 1 120); do
            kill -0 "$pid" 2>/dev/null || break
            sleep 0.5
        done

        if kill -0 "$pid" 2>/dev/null; then
            echo "Conay did not exit in time, update aborted."
        else
            for f in "$stage"/*; do
                mv -f "$f" "$app/" || echo "Failed to update $(basename "$f")"
            done
            chmod +x "$app/$exe"
            echo "Update applied."
        fi

        rm -rf "$stage"
        "$app/$exe" >/dev/null 2>&1 &
        """;

    [SupportedOSPlatform("linux")]
    private async Task DownloadLinuxArchive()
    {
        notifyService.UpdateStatus(this, "Downloading Conay update..");

        string appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string archivePath = Path.Combine(appDirectory, "conay-update.tar.gz");
        string stagingDir = Path.Combine(appDirectory, ".conay-update");
        string logPath = Path.Combine(appDirectory, "logs", "update.log");

        bool success = await http.Download(LinuxArchiveDownloadUrl, archivePath, new Progress<float>(ReportProgress));
        if (!success)
        {
            logger.LogError("Failed to download Conay update!");
            notifyService.UpdateStatus(this, "Failed to update Conay!");
            TryDelete(archivePath);
            return;
        }

        notifyService.UpdateStatus(this, "Verifying Conay update..");

        try
        {
            await Task.Run(() => ExtractLinuxUpdate(archivePath, stagingDir));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unpack Conay update!");
            notifyService.UpdateStatus(this, ex is UnauthorizedAccessException
                ? "Failed to update Conay (no write access to its folder)!"
                : "Failed to update Conay (downloaded update is invalid)!");
            TryDeleteDirectory(stagingDir);
            return;
        }
        finally
        {
            TryDelete(archivePath);
        }

        bool started;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            started = DetachedProcess.Start("conay",
            [
                "bash", "-c", LinuxUpdateScript.ReplaceLineEndings("\n"), "conay-update",
                Environment.ProcessId.ToString(), appDirectory, stagingDir, LinuxExecutableName
            ], logPath: logPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run update script!");
            started = false;
        }

        if (!started)
        {
            notifyService.UpdateStatus(this, "Failed to run update script!");
            TryDeleteDirectory(stagingDir);
            return;
        }

        notifyService.UpdateStatus(this, "Update downloaded, Conay will restart..");
        await Task.Delay(TimeSpan.FromSeconds(2));
        Environment.Exit(0);
    }

    [SupportedOSPlatform("linux")]
    private static void ExtractLinuxUpdate(string archivePath, string stagingDir)
    {
        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, true);
        Directory.CreateDirectory(stagingDir);

        using (FileStream file = File.OpenRead(archivePath))
        using (GZipStream gzip = new(file, CompressionMode.Decompress))
        {
            TarFile.ExtractToDirectory(gzip, stagingDir, overwriteFiles: true);
        }

        string executable = Path.Combine(stagingDir, LinuxExecutableName);
        if (!File.Exists(executable) || new FileInfo(executable).Length == 0)
            throw new InvalidDataException("Update archive does not contain the Conay executable.");

        File.SetUnixFileMode(executable, ExecutableMode);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignored
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // ignored
        }
    }
}