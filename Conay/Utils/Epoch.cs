using System;

namespace Conay.Utils;

public static class Epoch
{
    public static readonly DateTime UnixEpoch = DateTime.UnixEpoch;

    public static int Current => (int)DateTime.UtcNow.Subtract(UnixEpoch).TotalSeconds;

    public static DateTime ToDateTime(decimal unixTime) => UnixEpoch.AddSeconds((long)unixTime);

    public static uint FromDateTime(DateTime dt) => (uint)dt.Subtract(UnixEpoch).TotalSeconds;
}