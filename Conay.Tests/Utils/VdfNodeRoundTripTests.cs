using Conay.Utils;

namespace Conay.Tests.Utils;

// Random documents full of awkward text (quotes, backslashes, braces, comment markers, unicode) must come back
// from Steam's text format exactly as they went in
public class VdfNodeRoundTripTests
{
    private const string Characters = "abcXYZ019 _-.:/\\\"{}\t\n\r//éß☃漢🙂";

    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 50).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomDocument_SurvivesWritingAndReading(int seed)
    {
        Random random = new(seed);
        VdfNode document = new();
        List<(string[] Path, string Value)> values = [];
        Fill(random, document, [], 0, values);

        string text = document.ToString();
        VdfNode parsed = VdfNode.Parse(text);

        Assert.Equal(text, parsed.ToString());
        foreach ((string[] path, string value) in values)
        {
            VdfNode node = parsed;
            foreach (string key in path[..^1])
                node = node.GetNode(key)!;

            Assert.Equal(value, node.GetString(path[^1]));
        }
    }

    private static void Fill(Random random, VdfNode node, string[] path, int depth,
        List<(string[] Path, string Value)> values)
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        int entries = random.Next(depth == 0 ? 1 : 0, 6);

        for (int i = 0; i < entries; i++)
        {
            string key = RandomText(random, 1, 8);
            if (!keys.Add(key)) continue;

            string[] entryPath = [.. path, key];
            if (depth < 4 && random.Next(3) == 0)
            {
                Fill(random, node.GetOrAddNode(key), entryPath, depth + 1, values);
            }
            else
            {
                string value = RandomText(random, 0, 12);
                node.SetString(key, value);
                values.Add((entryPath, value));
            }
        }
    }

    private static string RandomText(Random random, int minLength, int maxLength)
    {
        int length = random.Next(minLength, maxLength + 1);
        return string.Concat(Enumerable.Range(0, length).Select(_ => Characters[random.Next(Characters.Length)]));
    }
}
