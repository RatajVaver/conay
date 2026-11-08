namespace Conay.Tests.TestSupport;

internal sealed class TempDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "conay-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory()
    {
        Directory.CreateDirectory(Root);
    }

    public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    public string Write(string relativePath, string contents)
    {
        string path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // ignored, a delayed config save can still be writing into it
        }
    }
}
