using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletHeader : UserControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(Title));

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> SubtitleProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(Subtitle));

        public string Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly StyledProperty<string> AppNameProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(AppName));

        public string AppName
        {
            get => GetValue(AppNameProperty);
            set => SetValue(AppNameProperty, value);
        }

        public static readonly StyledProperty<string> LogoPathProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(LogoPath));

        public string LogoPath
        {
            get => GetValue(LogoPathProperty);
            set => SetValue(LogoPathProperty, value);
        }

        public static readonly StyledProperty<bool> ShowLogoProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(ShowLogo));

        public bool ShowLogo
        {
            get => GetValue(ShowLogoProperty);
            set => SetValue(ShowLogoProperty, value);
        }

        public static readonly StyledProperty<bool> ShowAppNameProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(ShowAppName));

        public bool ShowAppName
        {
            get => GetValue(ShowAppNameProperty);
            set => SetValue(ShowAppNameProperty, value);
        }

        public static readonly StyledProperty<bool> ShowMenuButtonProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(ShowMenuButton));

        public bool ShowMenuButton
        {
            get => GetValue(ShowMenuButtonProperty);
            set => SetValue(ShowMenuButtonProperty, value);
        }

        public static readonly StyledProperty<bool> ShowAccountInfoProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(ShowAccountInfo));

        public bool ShowAccountInfo
        {
            get => GetValue(ShowAccountInfoProperty);
            set => SetValue(ShowAccountInfoProperty, value);
        }

        public static readonly StyledProperty<string> AccountNameProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(AccountName));

        public string AccountName
        {
            get => GetValue(AccountNameProperty);
            set => SetValue(AccountNameProperty, value);
        }

        public static readonly StyledProperty<string> AccountAddressProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(AccountAddress));

        public string AccountAddress
        {
            get => GetValue(AccountAddressProperty);
            set => SetValue(AccountAddressProperty, value);
        }

        public static readonly StyledProperty<string> NetworkNameProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(NetworkName));

        public string NetworkName
        {
            get => GetValue(NetworkNameProperty);
            set => SetValue(NetworkNameProperty, value);
        }

        public static readonly StyledProperty<string> NetworkLogoPathProperty =
            AvaloniaProperty.Register<WalletHeader, string>(nameof(NetworkLogoPath));

        public string NetworkLogoPath
        {
            get => GetValue(NetworkLogoPathProperty);
            set => SetValue(NetworkLogoPathProperty, value);
        }

        public static readonly StyledProperty<long> ChainIdProperty =
            AvaloniaProperty.Register<WalletHeader, long>(nameof(ChainId));

        public long ChainId
        {
            get => GetValue(ChainIdProperty);
            set => SetValue(ChainIdProperty, value);
        }

        public static readonly StyledProperty<bool> ShowNetworkInfoProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(ShowNetworkInfo), defaultValue: true);

        public bool ShowNetworkInfo
        {
            get => GetValue(ShowNetworkInfoProperty);
            set => SetValue(ShowNetworkInfoProperty, value);
        }

        public static readonly StyledProperty<bool> IsMobileProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(IsMobile));

        public bool IsMobile
        {
            get => GetValue(IsMobileProperty);
            set => SetValue(IsMobileProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactProperty =
            AvaloniaProperty.Register<WalletHeader, bool>(nameof(IsCompact));

        public bool IsCompact
        {
            get => GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        public static readonly StyledProperty<int> ComponentWidthProperty =
            AvaloniaProperty.Register<WalletHeader, int>(nameof(ComponentWidth), defaultValue: 400);

        public int ComponentWidth
        {
            get => GetValue(ComponentWidthProperty);
            set => SetValue(ComponentWidthProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnMenuClickCommandProperty =
            AvaloniaProperty.Register<WalletHeader, ICommand>(nameof(OnMenuClickCommand));

        public ICommand OnMenuClickCommand
        {
            get => GetValue(OnMenuClickCommandProperty);
            set => SetValue(OnMenuClickCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnAccountClickCommandProperty =
            AvaloniaProperty.Register<WalletHeader, ICommand>(nameof(OnAccountClickCommand));

        public ICommand OnAccountClickCommand
        {
            get => GetValue(OnAccountClickCommandProperty);
            set => SetValue(OnAccountClickCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnNetworkClickCommandProperty =
            AvaloniaProperty.Register<WalletHeader, ICommand>(nameof(OnNetworkClickCommand));

        public ICommand OnNetworkClickCommand
        {
            get => GetValue(OnNetworkClickCommandProperty);
            set => SetValue(OnNetworkClickCommandProperty, value);
        }

        public static readonly StyledProperty<object> ActionsContentProperty =
            AvaloniaProperty.Register<WalletHeader, object>(nameof(ActionsContent));

        public object ActionsContent
        {
            get => GetValue(ActionsContentProperty);
            set => SetValue(ActionsContentProperty, value);
        }

        public WalletHeader()
        {
            InitializeComponent();
        }

        public string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length <= 16)
                return address;

            return $"{address.Substring(0, 8)}...{address.Substring(address.Length - 6)}";
        }
    }
}
