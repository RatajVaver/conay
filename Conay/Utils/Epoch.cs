using System;

namespace Conay.Utils;

public static class Epoch
{
    public static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static int Current => (int)DateTime.UtcNow.Subtract(UnixEpoch).TotalSeconds;

    public static DateTime ToDateTime(decimal unixTime) => UnixEpoch.AddSeconds((long)unixTime);

    public static uint FromDateTime(DateTime dt) => (uint)dt.Subtract(UnixEpoch).TotalSeconds;
}