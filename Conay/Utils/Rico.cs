using System;
using System.IO;

namespace Conay.Utils;

public static class Rico
{
    public static string ExePath => Path.Combine(AppContext.BaseDirectory, "rico", "Rico.exe");

    public static bool IsInstalled => File.Exists(ExePath);
}
