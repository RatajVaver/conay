using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

// A Steam process seen while Steam starts up, ParentId is the process that started it. StartTime is null if it can't
// be inspected, ExitCode stays null while it runs.
internal sealed record SteamClientProcess(int Id, int ParentId, DateTime? StartTime, int? ExitCode);

// The Steam processes seen while watching (with their exit codes once they're gone, and the ones that can't be opened
// while they run), and starting Steam again
internal interface ISteamClientProcesses
{
    IReadOnlyList<SteamClientProcess> Poll();
    void Start();
}

// Keeps hold of each Steam process from when it's first seen, so its exit code can still be read after it's gone
[SupportedOSPlatform("windows")]
internal sealed class SteamClientProcesses(ILogger logger) : ISteamClientProcesses, IDisposable
{
    private readonly Dictionary<int, (Process Process, int ParentId, DateTime StartTime)> _tracked = [];

    internal string ProcessName { get; init; } = "steam";

    internal Action StartClient { get; init; } = () => Steam.StartClient(logger);

    public IReadOnlyList<SteamClientProcess> Poll()
    {
        Dictionary<int, int> parents = ProcessTree.ParentIds();
        List<SteamClientProcess> seen = [];

        foreach (Process process in Process.GetProcessesByName(ProcessName))
        {
            if (_tracked.ContainsKey(process.Id))
            {
                process.Dispose();
                continue;
            }

            int parentId = parents.GetValueOrDefault(process.Id);
            try
            {
                // Holding a handle keeps the exit code readable once it's gone, HasExited alone closes its own
                _ = process.Handle;
                if (!process.HasExited)
                {
                    _tracked[process.Id] = (process, parentId, process.StartTime);
                    continue;
                }
            }
            catch
            {
                // It can't be opened: Steam running as administrator, or the copy Windows makes of a crashed Steam
                seen.Add(new SteamClientProcess(process.Id, parentId, null, null));
            }

            process.Dispose();
        }

        foreach ((int id, (Process process, int parentId, DateTime startTime)) in _tracked)
        {
            try
            {
                seen.Add(new SteamClientProcess(id, parentId, startTime, process.HasExited ? process.ExitCode : null));
            }
            catch
            {
                // ignored, it can't be inspected anymore
            }
        }

        return seen;
    }

    public void Start() => StartClient();

    public void Dispose()
    {
        foreach ((Process process, int _, DateTime _) in _tracked.Values)
            process.Dispose();

        _tracked.Clear();
    }
}
