using System;

namespace Conay.Services;

public class NotifyService
{
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    public void UpdateStatus(object? sender, string message)
    {
        StatusChanged?.Invoke(sender, message);
    }

    public void UpdateProgress(object? sender, double progress)
    {
        DownloadProgressChanged?.Invoke(sender, progress);
    }
}