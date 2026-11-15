namespace Conay.Data;

public class SteamAccount
{
    public required ulong SteamId { get; init; }
    public required string AccountName { get; init; }
    public string PersonaName { get; init; } = string.Empty;
    public bool RememberPassword { get; init; }
    public bool MostRecent { get; init; }
    public string? AvatarPath { get; init; }

    public string DisplayName => string.IsNullOrEmpty(PersonaName) ? AccountName : PersonaName;
}
