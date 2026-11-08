using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Conay.Data;

namespace Conay.ViewModels.Parts;

public partial class SteamAccountItemViewModel : ViewModelBase
{
    private readonly SteamAccount _account;
    private readonly Func<SteamAccount, Task> _onSwitch;

    [ObservableProperty]
    private bool _isCurrent;

    public string DisplayName => _account.DisplayName;

    public string Details => _account.RememberPassword
        ? _account.AccountName
        : $"{_account.AccountName} · login not saved";

    public Bitmap? Avatar { get; }
    public bool HasAvatar => Avatar != null;

    public SteamAccountItemViewModel(SteamAccount account, bool isCurrent, Func<SteamAccount, Task> onSwitch)
    {
        _account = account;
        _onSwitch = onSwitch;
        IsCurrent = isCurrent;
        Avatar = Utils.Avatar.Load(account.AvatarPath);
    }

    [RelayCommand]
    private Task Switch() => _onSwitch(_account);
}
