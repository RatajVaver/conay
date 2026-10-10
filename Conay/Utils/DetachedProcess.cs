using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Conay.Utils;

[SupportedOSPlatform("linux")]
public static class DetachedProcess
{
    private const string Script =
        """
        if [ -n "$1" ]; then cd "$1" || exit 1; fi
        log="$2"; unit="$3"; shift 3
        command -v "$1" >/dev/null 2>&1 || exit 127
        if command -v systemd-run >/dev/null 2>&1 &&
            systemd-run --user --scope --quiet --collect true >/dev/null 2>&1; then
            set -- systemd-run --user --scope --quiet --collect --slice=app.slice --unit="$unit" -- "$@"
        fi
        if command -v setsid >/dev/null 2>&1; then
            setsid "$@" </dev/null >"$log" 2>&1 &
        else
            nohup "$@" </dev/null >"$log" 2>&1 &
        fi
        """;

    public static bool Start(string appId, IEnumerable<string> command, string? workingDirectory = null,
        string? logPath = null, IDictionary<string, string>? environment = null)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "bash",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(Script.ReplaceLineEndings("\n"));
        psi.ArgumentList.Add("conay-detach");
        psi.ArgumentList.Add(workingDirectory ?? string.Empty);
        psi.ArgumentList.Add(logPath ?? "/dev/null");
        psi.ArgumentList.Add($"app-{appId}-{Guid.NewGuid():N}.scope");
        foreach (string arg in command)
            psi.ArgumentList.Add(arg);

        foreach (string key in new[] { "SteamAppId", "SteamGameId", "SteamOverlayGameId" })
            psi.Environment.Remove(key);

        if (environment != null)
        {
            foreach (KeyValuePair<string, string> kv in environment)
                psi.Environment[kv.Key] = kv.Value;
        }

        using Process? process = Process.Start(psi);
        if (process == null) return false;

        process.WaitForExit(5000);
        return process is { HasExited: true, ExitCode: 0 };
    }
}