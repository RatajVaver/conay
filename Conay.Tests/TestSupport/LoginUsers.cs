namespace Conay.Tests.TestSupport;

// Builds loginusers.vdf contents in the exact format Steam writes
internal static class LoginUsers
{
    public static string File(params string[] users) => "\"users\"\n{\n" + string.Concat(users) + "}\n";

    public static string User(ulong steamId, string accountName, string personaName = "",
        bool rememberPassword = true, bool mostRecent = false) =>
        $"\t\"{steamId}\"\n" +
        "\t{\n" +
        $"\t\t\"AccountName\"\t\t\"{accountName}\"\n" +
        $"\t\t\"PersonaName\"\t\t\"{personaName}\"\n" +
        $"\t\t\"RememberPassword\"\t\t\"{(rememberPassword ? "1" : "0")}\"\n" +
        $"\t\t\"MostRecent\"\t\t\"{(mostRecent ? "1" : "0")}\"\n" +
        "\t\t\"Timestamp\"\t\t\"1760000000\"\n" +
        "\t}\n";
}
