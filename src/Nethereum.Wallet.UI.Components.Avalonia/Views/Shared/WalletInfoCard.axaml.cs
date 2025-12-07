using Avalonia;
using Avalonia.Controls;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletInfoCard : UserControl
    {
        public static readonly StyledProperty<WalletInfoSeverity> SeverityProperty =
            AvaloniaProperty.Register<WalletInfoCard, WalletInfoSeverity>(nameof(Severity), defaultValue: WalletInfoSeverity.Info);

        public WalletInfoSeverity Severity
        {
            get => GetValue(SeverityProperty);
            set => SetValue(SeverityProperty, value);
        }

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletInfoCard, string>(nameof(Title));

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<WalletInfoCard, string>(nameof(Description));

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly StyledProperty<string> IconProperty =
            AvaloniaProperty.Register<WalletInfoCard, string>(nameof(Icon));

        public string Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly StyledProperty<object> ChildContentProperty =
            AvaloniaProperty.Register<WalletInfoCard, object>(nameof(ChildContent));

        public object ChildContent
        {
            get => GetValue(ChildContentProperty);
            set => SetValue(ChildContentProperty, value);
        }

        public static readonly StyledProperty<object> ActionsProperty =
            AvaloniaProperty.Register<WalletInfoCard, object>(nameof(Actions));

        public object Actions
        {
            get => GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public WalletInfoCard()
        {
            InitializeComponent();
            this.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == SeverityProperty)
            {
                // Remove old severity class
                if (e.OldValue is WalletInfoSeverity oldSeverity)
                {
                    Classes.Remove(GetSeverityClass(oldSeverity));
                }
                // Add new severity class
                if (e.NewValue is WalletInfoSeverity newSeverity)
                {
                    Classes.Add(GetSeverityClass(newSeverity));
                }
            }
        }

        private string GetSeverityClass(WalletInfoSeverity severity) => severity switch
        {
            WalletInfoSeverity.Success => "success",
            WalletInfoSeverity.Warning => "warning",
            WalletInfoSeverity.Error => "error",
            _ => "info"
        };
    }

    public enum WalletInfoSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }
}
