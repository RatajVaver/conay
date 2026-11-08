using System;

namespace Conay.Data;

// What a Conay instance was started to do with Steam's account, passed on by the instance it replaced.
// SteamId 0 means adding an account, which leaves Steam on its sign-in screen.
public sealed record SwitchRequest(ulong SteamId)
{
    public const string SwitchArgument = "--switch-account";
    public const string AddAccountArgument = "--add-account";

    public static readonly SwitchRequest AddAccount = new(0UL);

    public static SwitchRequest? Current { get; } = FromArgs(Environment.GetCommandLineArgs());

    public bool IsAddAccount => SteamId == 0;

    public static SwitchRequest? FromArgs(string[] args)
    {
        if (Array.IndexOf(args, AddAccountArgument) >= 0)
            return AddAccount;

        int index = Array.IndexOf(args, SwitchArgument);
        return index >= 0 && index < args.Length - 1 && ulong.TryParse(args[index + 1], out ulong steamId) &&
               steamId != 0
            ? new SwitchRequest(steamId)
            : null;
    }

    public string[] ToArgs() => IsAddAccount ? [AddAccountArgument] : [SwitchArgument, SteamId.ToString()];
}
