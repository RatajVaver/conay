using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Conay.Utils;

public static class AppRestart
{
    private const string PreviousInstanceArgument = "--restarted-from";

    // Starts a new Conay instance with the given arguments and exits this one.
    // Returns only if the new instance could not be started.
    public static void Restart(params string[] args)
    {
        string? path = Environment.ProcessPath;
        if (path == null) return;

        List<string> command = BuildCommand(path, Assembly.GetEntryAssembly()?.Location, args, Environment.ProcessId);

        try
        {
            bool started = OperatingSystem.IsLinux()
                ? DetachedProcess.Start("conay", command, AppContext.BaseDirectory, startupEnvironment: true)
                : Start(command);

            if (!started) return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return;
        }

        Environment.Exit(0);
    }

    internal static List<string> BuildCommand(string processPath, string? entryAssemblyPath, string[] args,
        int processId)
    {
        List<string> command = [processPath];
        if (Path.GetFileNameWithoutExtension(processPath) == "dotnet" && entryAssemblyPath != null)
            command.Add(entryAssemblyPath);
        command.AddRange(args);
        command.AddRange([PreviousInstanceArgument, processId.ToString()]);
        return command;
    }

    internal static int? ParsePreviousInstance(string[] args)
    {
        int index = Array.IndexOf(args, PreviousInstanceArgument);
        return index >= 0 && index < args.Length - 1 && int.TryParse(args[index + 1], out int pid) ? pid : null;
    }

    // In a restarted instance, waits until the previous one has fully exited (or closes it after the timeout)
    public static void WaitForPreviousInstance(TimeSpan timeout)
    {
        if (ParsePreviousInstance(Environment.GetCommandLineArgs()) is not { } pid) return;

        try
        {
            using Process previous = Process.GetProcessById(pid);
            using Process current = Process.GetCurrentProcess();
            if (previous.ProcessName != current.ProcessName) return;

            if (!previous.WaitForExit(timeout))
                previous.Kill();
        }
        catch
        {
            // ignored, the previous instance is already gone
        }
    }

    private static bool Start(List<string> command)
    {
        using Process? process = Process.Start(CreateStartInfo(command, StartupEnvironment.Variables));
        return process != null;
    }

    // Without the game settings the Steam API put into this process, the new instance registers with Steam on its own
    // and passes nothing on to the Steam it starts
    internal static ProcessStartInfo CreateStartInfo(List<string> command,
        IReadOnlyDictionary<string, string> startupEnvironment)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = command[0],
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };

        foreach (string arg in command.Skip(1))
            startInfo.ArgumentList.Add(arg);

        StartupEnvironment.Apply(startInfo.Environment, startupEnvironment);
        return startInfo;
    }
}
