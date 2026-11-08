using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

// Steam can crash a few seconds into starting up: AMD's graphics driver puts a hook into it (GameManager64.dll)
// that breaks when Steam starts two programs at once, which it does while it starts. This watches a Steam that
// Conay started and starts it again after such a crash, until one has been running for a while.
internal sealed class SteamStartupWatcher(ILogger logger, NotifyService notifyService, ISteamClientProcesses processes)
{
    internal TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(1);

    // Steam crashed 1-12s after starting, one running for longer than this got through (Conay waits as long before
    // connecting to a Steam that's starting anyway)
    internal TimeSpan SettleTime { get; init; } = TimeSpan.FromSeconds(20);

    // A Steam that exited without crashing and doesn't come back was closed (after updating itself, Steam starts the
    // new version as it exits)
    internal TimeSpan RelaunchTimeout { get; init; } = TimeSpan.FromSeconds(15);

    internal int MaxRestarts { get; init; } = 5;

    internal Func<DateTime> Now { get; init; } = () => DateTime.Now;

    internal Func<TimeSpan, Task> Delay { get; init; } = Task.Delay;

    // Returns whether Steam got through starting up
    public async Task<bool> Watch()
    {
        int restarts = 0;
        DateTime? goneSince = null;
        HashSet<int> earlierStarts = [];

        while (true)
        {
            await Delay(CheckInterval);

            // Windows makes a copy of a crashing Steam to write the crash dump from, started by the crashed one and
            // left running for a while. It isn't a Steam to wait for, neither are the ones from before a restart.
            IReadOnlyList<SteamClientProcess> all = processes.Poll();
            List<SteamClientProcess> seen = all
                .Where(p => !earlierStarts.Contains(p.Id) && !earlierStarts.Contains(p.ParentId))
                .ToList();
            HashSet<int> crashedIds = seen.Where(IsCrash).Select(p => p.Id).ToHashSet();
            List<SteamClientProcess> steam = seen.Where(p => !crashedIds.Contains(p.ParentId)).ToList();

            List<SteamClientProcess> running = steam.Where(p => p.ExitCode == null).ToList();
            if (running.Count > 0)
            {
                goneSince = null;
                if (running.Any(p => p.StartTime is { } started && Now() - started >= SettleTime))
                {
                    logger.LogDebug("Steam got through starting up");
                    return true;
                }

                // One that can't be opened (e.g. Steam runs as administrator) can't be watched, unless it's that copy
                // of a Steam that's still crashing
                HashSet<int> runningIds = running.Select(p => p.Id).ToHashSet();
                if (running.Any(p => p.StartTime == null && !runningIds.Contains(p.ParentId)))
                {
                    logger.LogDebug("Steam can't be opened, so it can't be watched while it starts");
                    return true;
                }

                continue;
            }

            // Restarting while the crashed Steam itself is still there would only hand over to it
            goneSince ??= Now();
            SteamClientProcess? crashed = steam.FirstOrDefault(IsCrash);
            if (crashed == null && Now() - goneSince < RelaunchTimeout)
                continue;

            // It exited normally and didn't come back, so someone closed it
            if (crashed == null && steam.Count > 0)
            {
                logger.LogDebug("Steam was closed while it was starting");
                return false;
            }

            if (restarts == MaxRestarts)
            {
                logger.LogWarning("Steam kept crashing while starting, gave up after {Restarts} restarts", restarts);
                notifyService.UpdateStatus(this, "Steam keeps crashing while starting! Start it manually.");
                return false;
            }

            restarts++;
            if (crashed != null)
            {
                logger.LogWarning("Steam crashed while starting (exit code 0x{ExitCode:X8}), starting it again " +
                                  "({Restart}/{MaxRestarts})", crashed.ExitCode, restarts, MaxRestarts);
                notifyService.UpdateStatus(this, "Steam crashed while starting, starting it again..");
            }
            else
            {
                logger.LogWarning("Steam did not start, starting it again ({Restart}/{MaxRestarts})", restarts,
                    MaxRestarts);
                notifyService.UpdateStatus(this, "Steam did not start, starting it again..");
            }

            earlierStarts.UnionWith(all.Select(p => p.Id));
            processes.Start();
            goneSince = null;
        }
    }

    // Windows ends a crashed process with the exception's code (0xC0000005 for the crash in AMD's hook). Steam exits
    // with 0 when it's closed and 42 when it updated itself, a program that kills it leaves 1 or -1.
    private static bool IsCrash(SteamClientProcess process) =>
        process.ExitCode is { } code && (uint)code >= 0xC0000000 && code != -1;
}
