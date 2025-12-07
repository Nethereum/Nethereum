using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Nethereum.Wallet.WalletAccounts;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class AccountCard : UserControl
    {
        public static readonly StyledProperty<IWalletAccount> AccountProperty =
            AvaloniaProperty.Register<AccountCard, IWalletAccount>(nameof(Account));

        public IWalletAccount Account
        {
            get => GetValue(AccountProperty);
            set => SetValue(AccountProperty, value);
        }

        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<AccountCard, bool>(nameof(IsSelected));

        public bool IsSelected
        {
            get => GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly StyledProperty<bool> ShowActionsProperty =
            AvaloniaProperty.Register<AccountCard, bool>(nameof(ShowActions), defaultValue: true);

        public bool ShowActions
        {
            get => GetValue(ShowActionsProperty);
            set => SetValue(ShowActionsProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<AccountCard, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public static readonly StyledProperty<int> ComponentWidthProperty =
            AvaloniaProperty.Register<AccountCard, int>(nameof(ComponentWidth), defaultValue: 400);

        public int ComponentWidth
        {
            get => GetValue(ComponentWidthProperty);
            set => SetValue(ComponentWidthProperty, value);
        }

        public static readonly StyledProperty<string> AccountDisplayNameProperty =
            AvaloniaProperty.Register<AccountCard, string>(nameof(AccountDisplayName));

        public string AccountDisplayName
        {
            get => GetValue(AccountDisplayNameProperty);
            set => SetValue(AccountDisplayNameProperty, value);
        }

        public static readonly StyledProperty<string> AccountIconProperty =
            AvaloniaProperty.Register<AccountCard, string>(nameof(AccountIcon));

        public string AccountIcon
        {
            get => GetValue(AccountIconProperty);
            set => SetValue(AccountIconProperty, value);
        }

        public static readonly StyledProperty<string> AccountTypeDescriptionProperty =
            AvaloniaProperty.Register<AccountCard, string>(nameof(AccountTypeDescription));

        public string AccountTypeDescription
        {
            get => GetValue(AccountTypeDescriptionProperty);
            set => SetValue(AccountTypeDescriptionProperty, value);
        }

        public static readonly StyledProperty<string> FormattedAddressProperty =
            AvaloniaProperty.Register<AccountCard, string>(nameof(FormattedAddress));

        public string FormattedAddress
        {
            get => GetValue(FormattedAddressProperty);
            set => SetValue(FormattedAddressProperty, value);
        }

        public static readonly StyledProperty<IBrush> AccountColorProperty =
            AvaloniaProperty.Register<AccountCard, IBrush>(nameof(AccountColor), defaultValue: Brushes.Black);

        public IBrush AccountColor
        {
            get => GetValue(AccountColorProperty);
            set => SetValue(AccountColorProperty, value);
        }

        public static readonly StyledProperty<Func<string, Task<string>>> GetEnsNameAsyncProperty =
            AvaloniaProperty.Register<AccountCard, Func<string, Task<string>>>(nameof(GetEnsNameAsync));

        public Func<string, Task<string>> GetEnsNameAsync
        {
            get => GetValue(GetEnsNameAsyncProperty);
            set => SetValue(GetEnsNameAsyncProperty, value);
        }

        public static readonly StyledProperty<object> AdditionalChipsProperty =
            AvaloniaProperty.Register<AccountCard, object>(nameof(AdditionalChips));

        public object AdditionalChips
        {
            get => GetValue(AdditionalChipsProperty);
            set => SetValue(AdditionalChipsProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnViewDetailsCommandProperty =
            AvaloniaProperty.Register<AccountCard, ICommand>(nameof(OnViewDetailsCommand));

        public ICommand OnViewDetailsCommand
        {
            get => GetValue(OnViewDetailsCommandProperty);
            set => SetValue(OnViewDetailsCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnSelectCommandProperty =
            AvaloniaProperty.Register<AccountCard, ICommand>(nameof(OnSelectCommand));

        public ICommand OnSelectCommand
        {
            get => GetValue(OnSelectCommandProperty);
            set => SetValue(OnSelectCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnMenuCommandProperty =
            AvaloniaProperty.Register<AccountCard, ICommand>(nameof(OnMenuCommand));

        public ICommand OnMenuCommand
        {
            get => GetValue(OnMenuCommandProperty);
            set => SetValue(OnMenuCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnCopyAddressCommandProperty =
            AvaloniaProperty.Register<AccountCard, ICommand>(nameof(OnCopyAddressCommand));

        public ICommand OnCopyAddressCommand
        {
            get => GetValue(OnCopyAddressCommandProperty);
            set => SetValue(OnCopyAddressCommandProperty, value);
        }

        public static readonly StyledProperty<decimal> BalanceInEthProperty =
            AvaloniaProperty.Register<AccountCard, decimal>(nameof(BalanceInEth));

        public decimal BalanceInEth
        {
            get => GetValue(BalanceInEthProperty);
            set => SetValue(BalanceInEthProperty, value);
        }

        public static readonly StyledProperty<string> CurrencySymbolProperty =
            AvaloniaProperty.Register<AccountCard, string>(nameof(CurrencySymbol));

        public string CurrencySymbol
        {
            get => GetValue(CurrencySymbolProperty);
            set => SetValue(CurrencySymbolProperty, value);
        }
        public string DisplayAddress
        {
            get
            {
                if (!string.IsNullOrEmpty(FormattedAddress)) return FormattedAddress;
                if (string.IsNullOrEmpty(Account?.Address)) return "";

                if (IsCompactMode)
                {
                    if (Account.Address.Length > 16)
                    {
                        return $"{Account.Address.Substring(0, 6)}...{Account.Address.Substring(Account.Address.Length - 4)}";
                    }
                }
                return Account.Address;
            }
        }

        public string EnsName => _ensName;

        private string _ensName = "";
        private bool _ensLoading = false;

        public string IdenticonText => UI.Components.Utils.IdenticonGenerator.GetIdenticonText(Account?.Address);

        protected override async void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsCompactModeProperty)
            {
                if (change.NewValue is true)
                {
                    Classes.Add("compact");
                }
                else
                {
                    Classes.Remove("compact");
                }
            }

            if (change.Property == AccountProperty)
            {
                if (GetEnsNameAsync != null && !_ensLoading && string.IsNullOrEmpty(_ensName))
                {
                    _ensLoading = true;
                    try
                    {
                        var ensName = await GetEnsNameAsync(Account.Address);
                        if (!string.IsNullOrEmpty(ensName))
                        {
                            _ensName = ensName;
                            // This should trigger a UI update if the TextBlock is bound to a property that uses _ensName
                        }
                    }
                    catch
                    {
                        // ENS resolution failed, ignore
                    }
                    finally
                    {
                        _ensLoading = false;
                    }
                }
            }
        }
    }
}