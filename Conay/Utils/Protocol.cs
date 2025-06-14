using System;
using System.Diagnostics;

namespace Conay.Utils;

public static class Protocol
{
    public static void Open(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}