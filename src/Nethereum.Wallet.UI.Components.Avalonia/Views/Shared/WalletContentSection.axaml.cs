using Avalonia;
using Avalonia.Controls;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletContentSection : UserControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletContentSection, string>(nameof(Title));

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> SubtitleProperty =
            AvaloniaProperty.Register<WalletContentSection, string>(nameof(Subtitle));

        public string Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly StyledProperty<object> ChildContentProperty =
            AvaloniaProperty.Register<WalletContentSection, object>(nameof(ChildContent));

        public object ChildContent
        {
            get => GetValue(ChildContentProperty);
            set => SetValue(ChildContentProperty, value);
        }

        public static readonly StyledProperty<string> ClassProperty =
            AvaloniaProperty.Register<WalletContentSection, string>(nameof(Class), defaultValue: "");

        public string Class
        {
            get => GetValue(ClassProperty);
            set => SetValue(ClassProperty, value);
        }

        public static readonly StyledProperty<string> StyleProperty =
            AvaloniaProperty.Register<WalletContentSection, string>(nameof(Style), defaultValue: "");

        public string Style
        {
            get => GetValue(StyleProperty);
            set => SetValue(StyleProperty, value);
        }

        public static readonly StyledProperty<WalletSpacing> SpacingProperty =
            AvaloniaProperty.Register<WalletContentSection, WalletSpacing>(nameof(Spacing), defaultValue: WalletSpacing.Normal);

        public WalletSpacing Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public WalletContentSection()
        {
            InitializeComponent();
            this.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == SpacingProperty)
            {
                // Remove old spacing class
                if (e.OldValue is WalletSpacing oldSpacing)
                {
                    Classes.Remove(GetSpacingClass(oldSpacing));
                }
                // Add new spacing class
                if (e.NewValue is WalletSpacing newSpacing)
                {
                    Classes.Add(GetSpacingClass(newSpacing));
                }
            }
            else if (e.Property == ClassProperty)
            {
                // Remove old class
                if (e.OldValue is string oldClass && !string.IsNullOrEmpty(oldClass))
                {
                    Classes.Remove(oldClass);
                }
                // Add new class
                if (e.NewValue is string newClass && !string.IsNullOrEmpty(newClass))
                {
                    Classes.Add(newClass);
                }
            }
        }

        private string GetSpacingClass(WalletSpacing spacing) => spacing switch
        {
            WalletSpacing.Tight => "spacing-tight",
            WalletSpacing.Normal => "spacing-normal",
            WalletSpacing.Loose => "spacing-loose",
            _ => "spacing-normal"
        };
    }

    public enum WalletSpacing
    {
        Tight,
        Normal,
        Loose
    }
}
