using Conay.Utils;

namespace Conay.Tests.Utils;

public class VdfNodeTests
{
    private const string LoginUsers =
        "\"users\"\n{\n" +
        "\t\"76561198000000001\"\n\t{\n" +
        "\t\t\"AccountName\"\t\t\"alice_main\"\n" +
        "\t\t\"PersonaName\"\t\t\"Alice \\\"The Exile\\\" C:\\\\x\"\n" +
        "\t\t\"MostRecent\"\t\t\"1\"\n" +
        "\t}\n" +
        "\t\"76561198000000002\"\n\t{\n" +
        "\t\t\"AccountName\"\t\t\"alice_alt\"\n" +
        "\t\t\"PersonaName\"\t\t\"Ålice ☃ alt\"\n" +
        "\t\t\"mostrecent\"\t\t\"0\"\n" +
        "\t}\n" +
        "}\n";

    [Fact]
    public void Parse_ReadsNestedNodesInOrder()
    {
        VdfNode root = VdfNode.Parse(LoginUsers);

        List<(string Key, VdfNode Node)> users = root.GetNode("users")!.Children.ToList();

        Assert.Equal(new[] { "76561198000000001", "76561198000000002" }, users.Select(u => u.Key));
        Assert.Equal("alice_main", users[0].Node.GetString("AccountName"));
        Assert.Equal("alice_alt", users[1].Node.GetString("AccountName"));
    }

    [Fact]
    public void Parse_UnescapesQuotesAndBackslashes()
    {
        VdfNode user = VdfNode.Parse(LoginUsers).GetNode("users")!.GetNode("76561198000000001")!;

        Assert.Equal("Alice \"The Exile\" C:\\x", user.GetString("PersonaName"));
    }

    [Fact]
    public void Parse_KeepsUnicode()
    {
        VdfNode user = VdfNode.Parse(LoginUsers).GetNode("users")!.GetNode("76561198000000002")!;

        Assert.Equal("Ålice ☃ alt", user.GetString("PersonaName"));
    }

    [Fact]
    public void GetString_IgnoresKeyCasing()
    {
        VdfNode user = VdfNode.Parse(LoginUsers).GetNode("USERS")!.GetNode("76561198000000002")!;

        Assert.Equal("0", user.GetString("MostRecent"));
    }

    [Fact]
    public void GetString_ReturnsNullForMissingKeysAndNodes()
    {
        VdfNode root = VdfNode.Parse(LoginUsers);

        Assert.Null(root.GetString("missing"));
        Assert.Null(root.GetString("users"));
        Assert.Null(root.GetNode("users")!.GetNode("76561198000000001")!.GetNode("AccountName"));
    }

    [Fact]
    public void ToString_RoundTripsSteamFormattingExactly()
    {
        Assert.Equal(LoginUsers, VdfNode.Parse(LoginUsers).ToString());
    }

    [Fact]
    public void ToString_EscapesSpecialCharacters()
    {
        VdfNode root = new();
        root.SetString("value", "quote \" backslash \\ newline \n tab \t");

        VdfNode parsed = VdfNode.Parse(root.ToString());

        Assert.Equal("quote \" backslash \\ newline \n tab \t", parsed.GetString("value"));
    }

    [Fact]
    public void SetString_UpdatesExistingKeyAndKeepsItsCasing()
    {
        VdfNode root = VdfNode.Parse(LoginUsers);

        root.GetNode("users")!.GetNode("76561198000000002")!.SetString("MostRecent", "1");

        string text = root.ToString();
        Assert.Contains("\"mostrecent\"\t\t\"1\"", text);
        Assert.DoesNotContain("\"MostRecent\"\t\t\"0\"", text);
    }

    [Fact]
    public void SetString_AppendsMissingKey()
    {
        VdfNode root = VdfNode.Parse(LoginUsers);
        VdfNode user = root.GetNode("users")!.GetNode("76561198000000002")!;

        user.SetString("AllowAutoLogin", "1");

        Assert.Equal("1", VdfNode.Parse(root.ToString()).GetNode("users")!.GetNode("76561198000000002")!
            .GetString("AllowAutoLogin"));
    }

    [Fact]
    public void GetOrAddNode_ReusesExistingNodeRegardlessOfCasing()
    {
        VdfNode root = VdfNode.Parse(LoginUsers);

        VdfNode users = root.GetOrAddNode("Users");

        Assert.Same(root.GetNode("users"), users);
        Assert.Single(root.Children);
    }

    [Fact]
    public void GetOrAddNode_CreatesMissingPath()
    {
        VdfNode root = new();

        root.GetOrAddNode("Registry").GetOrAddNode("HKCU").SetString("AutoLoginUser", "alice_alt");

        VdfNode parsed = VdfNode.Parse(root.ToString());
        Assert.Equal("alice_alt", parsed.GetNode("Registry")!.GetNode("HKCU")!.GetString("AutoLoginUser"));
    }

    [Fact]
    public void GetOrAddNode_ReplacesStringValue()
    {
        VdfNode root = new();
        root.SetString("Apps", "nothing");

        VdfNode apps = root.GetOrAddNode("Apps");
        apps.SetString("440900", "1");

        Assert.Null(root.GetString("Apps"));
        Assert.Equal("1", root.GetNode("Apps")!.GetString("440900"));
    }

    [Fact]
    public void Children_SkipsPlainValues()
    {
        VdfNode root = VdfNode.Parse("\"a\"\t\t\"1\"\n\"b\"\n{\n}\n\"c\"\t\t\"2\"\n");

        Assert.Equal(new[] { "b" }, root.Children.Select(c => c.Key));
    }

    [Fact]
    public void Parse_IgnoresComments()
    {
        VdfNode root = VdfNode.Parse("// comment\n\"key\"\t\t\"value\" // trailing\n");

        Assert.Equal("value", root.GetString("key"));
    }

    [Fact]
    public void Parse_ReadsUnquotedTokens()
    {
        VdfNode root = VdfNode.Parse("Registry\n{\n\tRunningAppID 440900\n}\n");

        Assert.Equal("440900", root.GetNode("Registry")!.GetString("RunningAppID"));
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmptyNode()
    {
        VdfNode root = VdfNode.Parse(string.Empty);

        Assert.Empty(root.Children);
        Assert.Equal(string.Empty, root.ToString());
    }

    [Fact]
    public void Parse_UnterminatedNode_Throws()
    {
        Assert.Throws<FormatException>(() => VdfNode.Parse("\"users\"\n{\n\t\"a\"\t\t\"b\"\n"));
    }

    [Fact]
    public void Parse_UnexpectedClosingBrace_Throws()
    {
        Assert.Throws<FormatException>(() => VdfNode.Parse("\"a\"\t\t\"b\"\n}\n"));
    }

    [Fact]
    public void Parse_UnterminatedString_Throws()
    {
        Assert.Throws<FormatException>(() => VdfNode.Parse("\"key\"\t\t\"value"));
    }

    [Fact]
    public void Parse_KeyWithoutValue_Throws()
    {
        Assert.Throws<FormatException>(() => VdfNode.Parse("\"key\""));
    }
}
