using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public enum WalletTextFieldType
    {
        Text,
        Password,
        Email,
        Tel,
        Url,
        Search,
        Address,      // Ethereum address
        Mnemonic,     // Mnemonic phrase
        PrivateKey    // Private key
    }

    public partial class WalletTextField : UserControl, INotifyPropertyChanged
    {
        private readonly IComponentLocalizer _localizer;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Value Binding - with two-way binding by default
        public static readonly StyledProperty<string> ValueProperty =
            AvaloniaProperty.Register<WalletTextField, string>(
                nameof(Value),
                defaultValue: "",
                defaultBindingMode: BindingMode.TwoWay,
                coerce: OnValueCoerce);

        private static string OnValueCoerce(AvaloniaObject sender, string value)
        {
            if (sender is WalletTextField textField)
            {
                // Fire ValueChanged callback when Value changes
                textField.ValueChanged?.Invoke(value);

                // Fire PropertyChanged for computed properties
                textField.PropertyChanged?.Invoke(textField, new PropertyChangedEventArgs(nameof(Value)));
            }
            return value;
        }

        public string Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly StyledProperty<Action<string>> ValueChangedProperty =
            AvaloniaProperty.Register<WalletTextField, Action<string>>(nameof(ValueChanged));

        public Action<string> ValueChanged
        {
            get => GetValue(ValueChangedProperty);
            set => SetValue(ValueChangedProperty, value);
        }

        // TextField-Specific Parameters
        public static readonly StyledProperty<WalletTextFieldType> FieldTypeProperty =
            AvaloniaProperty.Register<WalletTextField, WalletTextFieldType>(nameof(FieldType), defaultValue: WalletTextFieldType.Text);

        public WalletTextFieldType FieldType
        {
            get => GetValue(FieldTypeProperty);
            set
            {
                SetValue(FieldTypeProperty, value);
                // Notify computed property changes
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComputedPasswordChar)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComputedActionIcon)));
            }
        }

        public static readonly StyledProperty<int> LinesProperty =
            AvaloniaProperty.Register<WalletTextField, int>(nameof(Lines), defaultValue: 1);

        public int Lines
        {
            get => GetValue(LinesProperty);
            set => SetValue(LinesProperty, value);
        }

        public static readonly StyledProperty<int> MaxLengthProperty =
            AvaloniaProperty.Register<WalletTextField, int>(nameof(MaxLength), defaultValue: 1000);

        public int MaxLength
        {
            get => GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public static readonly StyledProperty<string> ValidationPatternProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(ValidationPattern), defaultValue: "");

        public string ValidationPattern
        {
            get => GetValue(ValidationPatternProperty);
            set => SetValue(ValidationPatternProperty, value);
        }

        // Error handling parameters
        public static readonly StyledProperty<bool> ErrorProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(Error));

        public bool Error
        {
            get => GetValue(ErrorProperty);
            set => SetValue(ErrorProperty, value);
        }

        public static readonly StyledProperty<string> ErrorTextProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(ErrorText), defaultValue: "");

        public string ErrorText
        {
            get => GetValue(ErrorTextProperty);
            set => SetValue(ErrorTextProperty, value);
        }

        // Reveal toggle parameters
        public static readonly StyledProperty<bool> ShowRevealToggleProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(ShowRevealToggle));

        public bool ShowRevealToggle
        {
            get => GetValue(ShowRevealToggleProperty);
            set => SetValue(ShowRevealToggleProperty, value);
        }

        public static readonly StyledProperty<bool> IsRevealedProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(IsRevealed));

        public bool IsRevealed
        {
            get => GetValue(IsRevealedProperty);
            set
            {
                SetValue(IsRevealedProperty, value);
                // Notify computed property changes
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComputedPasswordChar)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComputedActionIcon)));
            }
        }

        public static readonly StyledProperty<ICommand> OnToggleRevealCommandProperty =
            AvaloniaProperty.Register<WalletTextField, ICommand>(nameof(OnToggleRevealCommand));

        public ICommand OnToggleRevealCommand
        {
            get => GetValue(OnToggleRevealCommandProperty);
            set => SetValue(OnToggleRevealCommandProperty, value);
        }

        public static readonly StyledProperty<string> ToggleRevealAriaLabelProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(ToggleRevealAriaLabel), defaultValue: "");

        public string ToggleRevealAriaLabel
        {
            get => GetValue(ToggleRevealAriaLabelProperty);
            set => SetValue(ToggleRevealAriaLabelProperty, value);
        }

        // Action icon parameters
        public static readonly StyledProperty<string> ActionIconProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(ActionIcon), defaultValue: "");

        public string ActionIcon
        {
            get => GetValue(ActionIconProperty);
            set => SetValue(ActionIconProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnActionClickCommandProperty =
            AvaloniaProperty.Register<WalletTextField, ICommand>(nameof(OnActionClickCommand));

        public ICommand OnActionClickCommand
        {
            get => GetValue(OnActionClickCommandProperty);
            set => SetValue(OnActionClickCommandProperty, value);
        }

        public static readonly StyledProperty<string> ActionTooltipProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(ActionTooltip), defaultValue: "");

        public string ActionTooltip
        {
            get => GetValue(ActionTooltipProperty);
            set => SetValue(ActionTooltipProperty, value);
        }

        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(IsLoading));

        public bool IsLoading
        {
            get => GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly StyledProperty<bool> IsMonospaceProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(IsMonospace));

        public bool IsMonospace
        {
            get => GetValue(IsMonospaceProperty);
            set => SetValue(IsMonospaceProperty, value);
        }

        public static readonly StyledProperty<string> SuffixProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(Suffix), defaultValue: "");

        public string Suffix
        {
            get => GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        // Localization Parameters (from WalletFormControlBase)
        public static readonly StyledProperty<string> LabelKeyProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(LabelKey), defaultValue: "");

        public string LabelKey
        {
            get => GetValue(LabelKeyProperty);
            set => SetValue(LabelKeyProperty, value);
        }

        public static readonly StyledProperty<string> HelpKeyProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(HelpKey), defaultValue: "");

        public string HelpKey
        {
            get => GetValue(HelpKeyProperty);
            set => SetValue(HelpKeyProperty, value);
        }

        public static readonly StyledProperty<string> PlaceholderKeyProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(PlaceholderKey), defaultValue: "");

        public string PlaceholderKey
        {
            get => GetValue(PlaceholderKeyProperty);
            set => SetValue(PlaceholderKeyProperty, value);
        }

        public static readonly StyledProperty<string> RequiredErrorKeyProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(RequiredErrorKey), defaultValue: "");

        public string RequiredErrorKey
        {
            get => GetValue(RequiredErrorKeyProperty);
            set => SetValue(RequiredErrorKeyProperty, value);
        }

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(Label), defaultValue: "");

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly StyledProperty<string> HelpTextProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(HelpText), defaultValue: "");

        public string HelpText
        {
            get => GetValue(HelpTextProperty);
            set => SetValue(HelpTextProperty, value);
        }

        public static readonly StyledProperty<string> PlaceholderProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(Placeholder), defaultValue: "");

        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly StyledProperty<string> RequiredErrorProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(RequiredError), defaultValue: "");

        public string RequiredError
        {
            get => GetValue(RequiredErrorProperty);
            set => SetValue(RequiredErrorProperty, value);
        }

        public static readonly StyledProperty<IComponentLocalizer> LocalizerProperty =
            AvaloniaProperty.Register<WalletTextField, IComponentLocalizer>(nameof(Localizer));

        public IComponentLocalizer Localizer
        {
            get => GetValue(LocalizerProperty);
            set => SetValue(LocalizerProperty, value);
        }

        // Common Form Parameters
        public static readonly StyledProperty<bool> RequiredProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(Required));

        public bool Required
        {
            get => GetValue(RequiredProperty);
            set => SetValue(RequiredProperty, value);
        }

        public static readonly StyledProperty<bool> LoadingProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(Loading));

        public bool Loading
        {
            get => GetValue(LoadingProperty);
            set => SetValue(LoadingProperty, value);
        }

        public static readonly StyledProperty<bool> DisabledProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(Disabled));

        public bool Disabled
        {
            get => GetValue(DisabledProperty);
            set => SetValue(DisabledProperty, value);
        }

        public static readonly StyledProperty<bool> ReadOnlyProperty =
            AvaloniaProperty.Register<WalletTextField, bool>(nameof(ReadOnly));

        public bool ReadOnly
        {
            get => GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public static readonly StyledProperty<string> ClassProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(Class), defaultValue: "");

        public string Class
        {
            get => GetValue(ClassProperty);
            set => SetValue(ClassProperty, value);
        }

        public static readonly StyledProperty<string> StyleProperty =
            AvaloniaProperty.Register<WalletTextField, string>(nameof(Style), defaultValue: "");

        public string Style
        {
            get => GetValue(StyleProperty);
            set => SetValue(StyleProperty, value);
        }

        // Password-specific properties
        public static readonly StyledProperty<char> PasswordCharProperty =
            AvaloniaProperty.Register<WalletTextField, char>(nameof(PasswordChar), defaultValue: '\0');

        public char PasswordChar
        {
            get => GetValue(PasswordCharProperty);
            set => SetValue(PasswordCharProperty, value);
        }

        // Computed properties for password handling
        public char ComputedPasswordChar
        {
            get
            {
                if (FieldType == WalletTextFieldType.Password || FieldType == WalletTextFieldType.PrivateKey)
                {
                    return IsRevealed ? '\0' : '●';
                }
                return '\0';
            }
        }

        public string ComputedActionIcon
        {
            get
            {
                if (ShowRevealToggle && (FieldType == WalletTextFieldType.Password || FieldType == WalletTextFieldType.PrivateKey))
                {
                    return IsRevealed ? "visibility_off" : "visibility";
                }

                if (!string.IsNullOrEmpty(ActionIcon))
                {
                    return ActionIcon;
                }

                return "";
            }
        }


        public ICommand HandleAdornmentClickCommand { get; private set; }

        public WalletTextField()
        {
            InitializeComponent();

            // Create a simple async command wrapper
            HandleAdornmentClickCommand = new AsyncRelayCommand(HandleAdornmentClick);

            // Set DataContext to self so internal bindings work
            DataContext = this;
        }

        public WalletTextField(IComponentLocalizer localizer)
        {
            InitializeComponent();
            _localizer = localizer;

            // Create a simple async command wrapper
            HandleAdornmentClickCommand = new AsyncRelayCommand(HandleAdornmentClick);

            // Set DataContext to self so internal bindings work
            DataContext = this;
        }

        // Helper Methods
        public string GetLabel()
        {
            if (!string.IsNullOrEmpty(Label))
                return Label;

            if (!string.IsNullOrEmpty(LabelKey) && Localizer != null)
                return Localizer.GetString(LabelKey);

            return "";
        }

        public string GetHelpText()
        {
            if (!string.IsNullOrEmpty(HelpText))
                return HelpText;

            if (!string.IsNullOrEmpty(HelpKey) && Localizer != null)
                return Localizer.GetString(HelpKey);

            return "";
        }

        public string GetPlaceholder()
        {
            if (!string.IsNullOrEmpty(Placeholder))
                return Placeholder;

            if (!string.IsNullOrEmpty(PlaceholderKey) && Localizer != null)
                return Localizer.GetString(PlaceholderKey);

            return "";
        }

        public string GetRequiredError()
        {
            if (!string.IsNullOrEmpty(RequiredError))
                return RequiredError;

            if (!string.IsNullOrEmpty(RequiredErrorKey) && Localizer != null)
                return Localizer.GetString(RequiredErrorKey);

            return "This field is required";
        }

        public bool IsDisabled()
        {
            return Disabled || Loading;
        }

        public string GetInputType()
        {
            // Handle reveal toggle for password and private key fields
            if (ShowRevealToggle && (FieldType == WalletTextFieldType.Password || FieldType == WalletTextFieldType.PrivateKey))
            {
                return IsRevealed ? "Text" : "Password";
            }

            return FieldType switch
            {
                WalletTextFieldType.Password => "Password",
                WalletTextFieldType.PrivateKey => "Password",
                WalletTextFieldType.Email => "Email",
                WalletTextFieldType.Tel => "Tel",
                WalletTextFieldType.Url => "Url",
                WalletTextFieldType.Search => "Search",
                _ => "Text"
            };
        }

        public bool HasActionButton()
        {
            return !string.IsNullOrEmpty(ActionIcon) && OnActionClickCommand != null;
        }

        public string GetAdornmentIcon()
        {
            if (ShowRevealToggle)
            {
                return IsRevealed ? "visibility_off" : "visibility";
            }

            if (FieldType == WalletTextFieldType.Search && !string.IsNullOrEmpty(Value))
            {
                return "clear";
            }

            if (HasActionButton())
            {
                return ActionIcon;
            }

            if (FieldType == WalletTextFieldType.Address && !string.IsNullOrEmpty(Value))
            {
                return "content_copy";
            }

            return "";
        }

        public async Task HandleAdornmentClick()
        {
            if (ShowRevealToggle && (FieldType == WalletTextFieldType.Password || FieldType == WalletTextFieldType.PrivateKey))
            {
                // Handle reveal toggle - if no command provided, use default behavior
                if (OnToggleRevealCommand?.CanExecute(null) == true)
                {
                    OnToggleRevealCommand.Execute(null);
                }
                else
                {
                    // Default toggle behavior
                    IsRevealed = !IsRevealed;
                }
            }
            else if (FieldType == WalletTextFieldType.Search && !string.IsNullOrEmpty(Value))
            {
                // Clear search field
                ValueChanged?.Invoke("");
            }
            else if (HasActionButton())
            {
                if (OnActionClickCommand?.CanExecute(null) == true)
                {
                    OnActionClickCommand.Execute(null);
                }
            }
            else if (FieldType == WalletTextFieldType.Address && !string.IsNullOrEmpty(Value))
            {
                await CopyToClipboard(Value);
            }
        }

        private async Task CopyToClipboard(string text)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }

        public string GetAdornmentAriaLabel()
        {
            if (ShowRevealToggle)
                return ToggleRevealAriaLabel;

            if (FieldType == WalletTextFieldType.Search && !string.IsNullOrEmpty(Value))
                return "Clear search";

            if (HasActionButton())
                return ActionTooltip;

            return "";
        }

        public string GetFieldStyle()
        {
            var baseStyle = Style ?? "";
            if (IsMonospace)
            {
                var monoStyle = "font-family: 'JetBrains Mono', 'SF Mono', 'Monaco', 'Consolas', monospace;";
                // This is a string, so it will be applied as a direct style. Avalonia prefers styles via selectors.
                // For now, we'll return the string, but ideally this would be handled via a style selector.
                return string.IsNullOrEmpty(baseStyle) ? monoStyle : $"{baseStyle} {monoStyle}";
            }
            return baseStyle;
        }

        public enum WalletTextFieldType
        {
            Text,
            Password,
            Email,
            Tel,
            Url,
            Search,
            Address,      // Ethereum address
            Mnemonic,     // Mnemonic phrase
            PrivateKey    // Private key
        }
    }
}
