using Avalonia.Controls;
using Conay.ViewModels.Parts;

namespace Conay.Tests.ViewModels;

public class SteamAccountOptionTests
{
    [Fact]
    public void SelectedChoice_IsCheckedRadioItem()
    {
        SteamAccountOption option = new("Alt", () => { }, true);

        Assert.Equal(MenuItemToggleType.Radio, option.ToggleType);
        Assert.True(option.IsSelected);
    }

    [Fact]
    public void UnselectedChoice_IsUncheckedRadioItem()
    {
        SteamAccountOption option = new("Alt", () => { }, false);

        Assert.Equal(MenuItemToggleType.Radio, option.ToggleType);
        Assert.False(option.IsSelected);
    }

    [Fact]
    public void Action_IsPlainItem()
    {
        SteamAccountOption option = new("Add account…", () => { });

        Assert.Equal(MenuItemToggleType.None, option.ToggleType);
        Assert.False(option.IsSelected);
    }

    [Fact]
    public void SelectCommand_RunsAction()
    {
        int calls = 0;
        SteamAccountOption option = new("Alt", () => calls++, false);

        option.SelectCommand.Execute(null);

        Assert.Equal(1, calls);
    }
}
