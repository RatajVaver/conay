using Conay.Data;

namespace Conay.Tests.Data;

public class SwitchRequestTests
{
    private const ulong Alt = 76561198000000002;

    [Fact]
    public void FromArgs_ReadsAccountToSwitchTo()
    {
        SwitchRequest? request = SwitchRequest.FromArgs(
            ["Conay.exe", "--server", "arcanasgrasp", SwitchRequest.SwitchArgument, Alt.ToString(), "--restarted-from", "7"]);

        Assert.Equal(new SwitchRequest(Alt), request);
        Assert.False(request!.IsAddAccount);
    }

    [Fact]
    public void FromArgs_ReadsAddingAnAccount()
    {
        SwitchRequest? request = SwitchRequest.FromArgs(["Conay.exe", SwitchRequest.AddAccountArgument]);

        Assert.Same(SwitchRequest.AddAccount, request);
        Assert.True(request!.IsAddAccount);
    }

    [Fact]
    public void FromArgs_WithoutArgument_IsNothing()
    {
        Assert.Null(SwitchRequest.FromArgs(["Conay.exe", "--server", "arcanasgrasp"]));
    }

    [Fact]
    public void FromArgs_WithoutAccount_IsNothing()
    {
        Assert.Null(SwitchRequest.FromArgs(["Conay.exe", SwitchRequest.SwitchArgument]));
    }

    [Fact]
    public void FromArgs_WithInvalidAccount_IsNothing()
    {
        Assert.Null(SwitchRequest.FromArgs(["Conay.exe", SwitchRequest.SwitchArgument, "alt"]));
        Assert.Null(SwitchRequest.FromArgs(["Conay.exe", SwitchRequest.SwitchArgument, "0"]));
    }

    [Fact]
    public void ToArgs_RoundTrips()
    {
        Assert.Equal(new SwitchRequest(Alt), SwitchRequest.FromArgs(new SwitchRequest(Alt).ToArgs()));
        Assert.Equal(SwitchRequest.AddAccount, SwitchRequest.FromArgs(SwitchRequest.AddAccount.ToArgs()));
    }
}
