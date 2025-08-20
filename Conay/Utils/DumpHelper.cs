using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Conay.Utils;

public static class DumpHelper
{
    public static void Enable()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    public static void Disable()
    {
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
    }

    [DllImport("dbghelp.dll")]
    private static extern bool MiniDumpWriteDump(IntPtr hProcess,
        int processId,
        IntPtr hFile,
        int dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        CreateMiniDump(e.ExceptionObject as Exception);
    }

    private static void CreateMiniDump(Exception? ex)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dumpFile = $"logs/crash_{timestamp}.dmp";
        string logFile = $"logs/crash_{timestamp}.log";

        try
        {
            if (!Directory.Exists("logs"))
                Directory.CreateDirectory("logs");
        }
        catch
        {
            // ignored
        }

        try
        {
            File.WriteAllText(logFile, ex?.ToString());
        }
        catch
        {
            // ignored
        }

        try
        {
            using FileStream fs = new(dumpFile, FileMode.Create);
            using Process process = Process.GetCurrentProcess();

            MiniDumpWriteDump(process.Handle,
                process.Id,
                fs.SafeFileHandle.DangerousGetHandle(),
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }
        catch
        {
            // ignored
        }

        try
        {
            MessageBox(IntPtr.Zero,
                $"Application crashed - please report this issue!\n\nDetails: {ex?.Message}\n\nDump created at:\n{Path.GetFullPath(dumpFile)}",
                "Conay",
                0x00000010);
        }
        catch
        {
            // ignored
        }
    }
}