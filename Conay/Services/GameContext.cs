using System;
using Conay.Data;

namespace Conay.Services;

public class GameContext
{
    public GameVersion Version { get; }
    public uint AppId => GameVersionHelper.GetAppId(Version);

    public GameContext()
    {
        Version = ParseVersion(Environment.GetCommandLineArgs());
    }

    private static GameVersion ParseVersion(string[] args)
    {
        int idx = Array.FindIndex(args, x => x is "--game" or "-g");
        if (idx >= 0 && idx < args.Length - 1)
            return GameVersionHelper.FromString(args[idx + 1]);

        return GameVersion.Legacy;
    }
}
