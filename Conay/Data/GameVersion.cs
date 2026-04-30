namespace Conay.Data;

public enum GameVersion
{
    Legacy,
    Enhanced
}

public static class GameVersionHelper
{
    public const uint AppId = 440900;
    public static GameVersion Current { get; set; } = GameVersion.Enhanced;

    public static GameVersion FromSteamBranch(string? value) => value?.ToLowerInvariant() switch
    {
        "conan-exiles-legacy" => GameVersion.Legacy,
        _ => GameVersion.Enhanced
    };

    public static GameVersion FromPresetVersion(string? value) => value?.ToLowerInvariant() switch
    {
        "enhanced" => GameVersion.Enhanced,
        _ => GameVersion.Legacy
    };

    public static string ToDisplayName(GameVersion version) => version switch
    {
        GameVersion.Legacy => "Legacy",
        _ => "Enhanced"
    };
}
