using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class AccountTypeCard : UserControl
    {
        public static readonly StyledProperty<IAccountCreationViewModel> AccountTypeProperty =
            AvaloniaProperty.Register<AccountTypeCard, IAccountCreationViewModel>(nameof(AccountType));

        public IAccountCreationViewModel AccountType
        {
            get => GetValue(AccountTypeProperty);
            set => SetValue(AccountTypeProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<AccountTypeCard, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnSelectCommandProperty =
            AvaloniaProperty.Register<AccountTypeCard, ICommand>(nameof(OnSelectCommand));

        public ICommand OnSelectCommand
        {
            get => GetValue(OnSelectCommandProperty);
            set => SetValue(OnSelectCommandProperty, value);
        }
        public AccountTypeCard()
        {
            this.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsCompactModeProperty)
            {
                if (e.NewValue is bool isCompact && isCompact)
                {
                    Classes.Add("compact");
                }
                else
                {
                    Classes.Remove("compact");
                }
            }
        }
        private void OnCardPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (OnSelectCommand?.CanExecute(AccountType) == true)
            {
                OnSelectCommand.Execute(AccountType);
            }
        }
    }
}
