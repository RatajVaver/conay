using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Data;
using Conay.Services;

namespace Conay.ViewModels;

public partial class DualInstallWizardViewModel(Steam steam) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    private int _step;

    [ObservableProperty] private string _errorMessage = string.Empty;

    public string CustomLegacyDir => steam.CustomLegacyDir;
    public bool HasCustomLegacyDir => !string.IsNullOrEmpty(steam.CustomLegacyDir);

    public bool IsStep0 => Step == 0;
    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;

    public bool IsAlreadyActive => steam.DualInstallMode;
    public bool IsConayInsideGameDir => !steam.DualInstallMode && CheckConayInsideInstall();
    public bool IsGameRunning => !steam.DualInstallMode && !CheckConayInsideInstall() && steam.IsGameRunning;
    public bool IsGameUpdating => !steam.DualInstallMode && !CheckConayInsideInstall() && !steam.IsGameRunning && steam.IsGameDownloading;
    public bool IsReadyForInstall => !steam.DualInstallMode && !CheckConayInsideInstall() && !steam.IsGameRunning && !steam.IsGameDownloading;

    private bool CheckConayInsideInstall()
    {
        if (string.IsNullOrEmpty(steam.AppInstallDir)) return false;
        string appDir = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string gameDir = Path.GetFullPath(steam.AppInstallDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return appDir.StartsWith(gameDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || appDir.Equals(gameDir, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEnhancedCurrent => GameVersionHelper.Current == GameVersion.Enhanced;

    public string CurrentVersion => GameVersionHelper.ToDisplayName(GameVersionHelper.Current);
    public string OtherVersion => GameVersionHelper.Current == GameVersion.Enhanced ? "Legacy" : "Enhanced";

    public string EnhancedPath => steam.GetInstallDirForVersion(GameVersion.Enhanced);
    public string LegacyPath => steam.GetInstallDirForVersion(GameVersion.Legacy);

    public string SourcePath => steam.AppInstallDir;

    public string DestPath => string.IsNullOrEmpty(steam.AppInstallDir)
        ? string.Empty
        : Path.Combine(Path.GetFullPath(Path.Combine(steam.AppInstallDir, "..")),
            $"Conan Exiles {CurrentVersion}");

    [RelayCommand]
    private void Start()
    {
        if (CheckConayInsideInstall() || steam.IsGameRunning || steam.IsGameDownloading) return;
        if (!string.IsNullOrEmpty(DestPath) && Directory.Exists(DestPath))
        {
            Step = 2;
            return;
        }
        Step = 1;
    }

    [RelayCommand]
    private void RenameFolder()
    {
        ErrorMessage = string.Empty;

        if (steam.IsGameRunning)
        {
            ErrorMessage = "Conan Exiles is still running. Close the game before proceeding.";
            return;
        }

        if (steam.IsGameDownloading)
        {
            ErrorMessage = "Steam is still downloading Conan Exiles. Wait for all updates to finish first.";
            return;
        }

        if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestPath))
        {
            ErrorMessage = "Could not determine install path. Is Steam running?";
            return;
        }

        if (!Directory.Exists(SourcePath))
        {
            ErrorMessage = $"Source folder not found:\n{SourcePath}";
            return;
        }

        if (Directory.Exists(DestPath))
        {
            Step = 2;
            return;
        }

        try
        {
            Directory.Move(SourcePath, DestPath);

            string steamApps = Path.GetFullPath(Path.Combine(SourcePath, "../.."));
            string acf = Path.Combine(steamApps, $"appmanifest_{GameVersionHelper.AppId}.acf");
            if (File.Exists(acf))
                File.Move(acf, acf + ".bak", overwrite: true);

            Step = 2;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to rename folder: {ex.Message}" +
                           $"\nTry relaunching Conay as an administrator.";
        }
    }
}