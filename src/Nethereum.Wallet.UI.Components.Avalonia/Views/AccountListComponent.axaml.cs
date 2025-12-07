using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.AccountList;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views;

public partial class AccountListComponent : UserControl
{
    public static readonly StyledProperty<AccountListPluginViewModel> ViewModelProperty =
        AvaloniaProperty.Register<AccountListComponent, AccountListPluginViewModel>(nameof(ViewModel));

    public AccountListPluginViewModel ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public AccountListComponent() => InitializeComponent();
}
