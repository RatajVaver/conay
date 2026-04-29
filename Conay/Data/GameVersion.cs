namespace Conay.Data;

public enum GameVersion
{
    Legacy,
    Enhanced
}

public static class GameVersionHelper
{
    public const uint LegacyAppId = 440900;
    public const uint EnhancedAppId = 0; // TODO: fill this in

    public static uint GetAppId(GameVersion version) => version switch
    {
        GameVersion.Enhanced => EnhancedAppId,
        _ => LegacyAppId
    };

    public static GameVersion FromString(string? value) => value?.ToLowerInvariant() switch
    {
        "enhanced" => GameVersion.Enhanced,
        _ => GameVersion.Legacy
    };

    public static string ToArg(GameVersion version) => version switch
    {
        GameVersion.Enhanced => "enhanced",
        _ => "legacy"
    };

    public static string ToDisplayName(GameVersion version) => version switch
    {
        GameVersion.Enhanced => "Enhanced",
        _ => "Legacy"
    };
}
