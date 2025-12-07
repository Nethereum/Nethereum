using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class AccountTypeSelectionComponent : UserControl
    {
        public static readonly StyledProperty<IEnumerable<IAccountCreationViewModel>> AvailableAccountTypesProperty =
            AvaloniaProperty.Register<AccountTypeSelectionComponent, IEnumerable<IAccountCreationViewModel>>(nameof(AvailableAccountTypes), defaultValue: Enumerable.Empty<IAccountCreationViewModel>());

        public IEnumerable<IAccountCreationViewModel> AvailableAccountTypes
        {
            get => GetValue(AvailableAccountTypesProperty);
            set => SetValue(AvailableAccountTypesProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<AccountTypeSelectionComponent, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnAccountTypeSelectedCommandProperty =
            AvaloniaProperty.Register<AccountTypeSelectionComponent, ICommand>(nameof(OnAccountTypeSelectedCommand));

        public ICommand OnAccountTypeSelectedCommand
        {
            get => GetValue(OnAccountTypeSelectedCommandProperty);
            set => SetValue(OnAccountTypeSelectedCommandProperty, value);
        }
    }
}
