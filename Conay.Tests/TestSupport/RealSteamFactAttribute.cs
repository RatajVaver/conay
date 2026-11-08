namespace Conay.Tests.TestSupport;

// For tests that close and start the real Steam on this machine, they only run when asked for
public sealed class RealSteamFactAttribute : FactAttribute
{
    public const string Variable = "CONAY_REAL_STEAM";

    public RealSteamFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows only";
        else if (Environment.GetEnvironmentVariable(Variable) != "1")
            Skip = $"Closes and starts the real Steam, set {Variable}=1 to run it";
    }
}

// There's only one Steam on this machine, so tests on it run one at a time and after all the others
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealSteamCollection
{
    public const string Name = "Real Steam";
}
