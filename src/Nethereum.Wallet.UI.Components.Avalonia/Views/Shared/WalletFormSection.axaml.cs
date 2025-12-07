using Avalonia;
using Avalonia.Controls;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletFormSection : UserControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletFormSection, string>(nameof(Title));

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<object> ChildContentProperty =
            AvaloniaProperty.Register<WalletFormSection, object>(nameof(ChildContent));

        public object ChildContent
        {
            get => GetValue(ChildContentProperty);
            set => SetValue(ChildContentProperty, value);
        }

        public WalletFormSection()
        {
            InitializeComponent();
        }
    }
}
