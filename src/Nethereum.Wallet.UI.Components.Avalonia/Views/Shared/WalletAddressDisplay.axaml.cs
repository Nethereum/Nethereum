using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Controls.Primitives;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletAddressDisplay : UserControl
    {
        public static readonly StyledProperty<string> AddressProperty =
            AvaloniaProperty.Register<WalletAddressDisplay, string>(nameof(Address));

        public string Address
        {
            get => GetValue(AddressProperty);
            set => SetValue(AddressProperty, value);
        }

        public static readonly StyledProperty<int> ComponentWidthProperty =
            AvaloniaProperty.Register<WalletAddressDisplay, int>(nameof(ComponentWidth), defaultValue: 800);

        public int ComponentWidth
        {
            get => GetValue(ComponentWidthProperty);
            set => SetValue(ComponentWidthProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactProperty =
            AvaloniaProperty.Register<WalletAddressDisplay, bool>(nameof(IsCompact));

        public bool IsCompact
        {
            get => GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        public static readonly StyledProperty<bool> ShowFullAddressProperty =
            AvaloniaProperty.Register<WalletAddressDisplay, bool>(nameof(ShowFullAddress));

        public bool ShowFullAddress
        {
            get => GetValue(ShowFullAddressProperty);
            set => SetValue(ShowFullAddressProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnCopyCommandProperty =
            AvaloniaProperty.Register<WalletAddressDisplay, ICommand>(nameof(OnCopyCommand));

        public ICommand OnCopyCommand
        {
            get => GetValue(OnCopyCommandProperty);
            set => SetValue(OnCopyCommandProperty, value);
        }

        public WalletAddressDisplay()
        {
            InitializeComponent();
        }

        public string GetDisplayAddress()
        {
            if (ShowFullAddress)
                return Address;

            if (string.IsNullOrEmpty(Address) || Address.Length < 10)
                return Address;

            if (IsCompact)
            {
                if (Address.Length > 16)
                {
                    return $"{Address.Substring(0, 6)}...{Address.Substring(Address.Length - 4)}";
                }
            }

            return Address;
        }

        public async Task HandleCopyClick()
        {
            if (!string.IsNullOrEmpty(Address))
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(Address);
                }
                OnCopyCommand?.Execute(Address);
            }
        }
    }
}
