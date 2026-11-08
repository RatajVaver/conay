using Conay.Data;
using Conay.ViewModels.Parts;

namespace Conay.Tests.ViewModels;

public class SteamAccountItemViewModelTests
{
    private static SteamAccount Account(bool rememberPassword = true) => new()
    {
        SteamId = 76561198000000002,
        AccountName = "alice_alt",
        PersonaName = "Alice",
        RememberPassword = rememberPassword,
        AvatarPath = Path.Combine(Path.GetTempPath(), $"conay-missing-{Guid.NewGuid():N}.png")
    };

    private static SteamAccountItemViewModel Create(SteamAccount account, bool isCurrent = false,
        Func<SteamAccount, Task>? onSwitch = null) =>
        new(account, isCurrent, onSwitch ?? (_ => Task.CompletedTask));

    [Fact]
    public void DisplayName_UsesPersonaName()
    {
        Assert.Equal("Alice", Create(Account()).DisplayName);
    }

    [Fact]
    public void Details_ShowsLoginName()
    {
        Assert.Equal("alice_alt", Create(Account()).Details);
    }

    [Fact]
    public void Details_FlagsAccountsWithoutSavedLogin()
    {
        Assert.Equal("alice_alt · login not saved", Create(Account(rememberPassword: false)).Details);
    }

    [Fact]
    public void Avatar_IsMissingWhenSteamHasNoCachedImage()
    {
        SteamAccountItemViewModel item = Create(Account());

        Assert.Null(item.Avatar);
        Assert.False(item.HasAvatar);
    }

    [Fact]
    public void IsCurrent_ComesFromCaller()
    {
        Assert.True(Create(Account(), isCurrent: true).IsCurrent);
        Assert.False(Create(Account(), isCurrent: false).IsCurrent);
    }

    [Fact]
    public async Task SwitchCommand_SwitchesToThisAccount()
    {
        SteamAccount account = Account();
        SteamAccount? switchedTo = null;
        SteamAccountItemViewModel item = Create(account, onSwitch: a =>
        {
            switchedTo = a;
            return Task.CompletedTask;
        });

        await item.SwitchCommand.ExecuteAsync(null);

        Assert.Same(account, switchedTo);
    }
}
