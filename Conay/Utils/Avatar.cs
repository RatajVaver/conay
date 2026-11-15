using System.IO;
using Avalonia.Media.Imaging;

namespace Conay.Utils;

public static class Avatar
{
    public static Bitmap? Load(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }
}
