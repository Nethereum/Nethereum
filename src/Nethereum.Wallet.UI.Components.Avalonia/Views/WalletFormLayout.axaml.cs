using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class WalletFormLayout : UserControl
    {
        private readonly IComponentLocalizer _localizer;

        public IComponentLocalizer Localizer => _localizer;

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletFormLayout, string>(nameof(Title));

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> SubtitleProperty =
            AvaloniaProperty.Register<WalletFormLayout, string>(nameof(Subtitle));

        public string Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly StyledProperty<IEnumerable<WalletFormStep>> StepsProperty =
            AvaloniaProperty.Register<WalletFormLayout, IEnumerable<WalletFormStep>>(nameof(Steps));

        public IEnumerable<WalletFormStep> Steps
        {
            get => GetValue(StepsProperty);
            set => SetValue(StepsProperty, value);
        }

        public static readonly StyledProperty<int> CurrentStepIndexProperty =
            AvaloniaProperty.Register<WalletFormLayout, int>(nameof(CurrentStepIndex));

        public int CurrentStepIndex
        {
            get => GetValue(CurrentStepIndexProperty);
            set => SetValue(CurrentStepIndexProperty, value);
        }

        public static readonly StyledProperty<string> ExitTextProperty =
            AvaloniaProperty.Register<WalletFormLayout, string>(nameof(ExitText));

        public string ExitText
        {
            get => GetValue(ExitTextProperty);
            set => SetValue(ExitTextProperty, value);
        }

        public static readonly StyledProperty<string> BackTextProperty =
            AvaloniaProperty.Register<WalletFormLayout, string>(nameof(BackText));

        public string BackText
        {
            get => GetValue(BackTextProperty);
            set => SetValue(BackTextProperty, value);
        }

        public static readonly StyledProperty<string> ContinueTextProperty =
            AvaloniaProperty.Register<WalletFormLayout, string>(nameof(ContinueText));

        public string ContinueText
        {
            get => GetValue(ContinueTextProperty);
            set => SetValue(ContinueTextProperty, value);
        }

        public static readonly StyledProperty<string> PrimaryTextProperty =
            AvaloniaProperty.Register<WalletFormLayout, string>(nameof(PrimaryText));

        public string PrimaryText
        {
            get => GetValue(PrimaryTextProperty);
            set => SetValue(PrimaryTextProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackProperty =
            AvaloniaProperty.Register<WalletFormLayout, bool>(nameof(ShowBack));

        public bool ShowBack
        {
            get => GetValue(ShowBackProperty);
            set => SetValue(ShowBackProperty, value);
        }

        public static readonly StyledProperty<bool> ShowContinueProperty =
            AvaloniaProperty.Register<WalletFormLayout, bool>(nameof(ShowContinue));

        public bool ShowContinue
        {
            get => GetValue(ShowContinueProperty);
            set => SetValue(ShowContinueProperty, value);
        }

        public static readonly StyledProperty<bool> ShowPrimaryProperty =
            AvaloniaProperty.Register<WalletFormLayout, bool>(nameof(ShowPrimary));

        public bool ShowPrimary
        {
            get => GetValue(ShowPrimaryProperty);
            set => SetValue(ShowPrimaryProperty, value);
        }

        public static readonly StyledProperty<bool> ContinueDisabledProperty =
            AvaloniaProperty.Register<WalletFormLayout, bool>(nameof(ContinueDisabled));

        public bool ContinueDisabled
        {
            get => GetValue(ContinueDisabledProperty);
            set => SetValue(ContinueDisabledProperty, value);
        }

        public static readonly StyledProperty<bool> PrimaryDisabledProperty =
            AvaloniaProperty.Register<WalletFormLayout, bool>(nameof(PrimaryDisabled));

        public bool PrimaryDisabled
        {
            get => GetValue(PrimaryDisabledProperty);
            set => SetValue(PrimaryDisabledProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnExitCommandProperty =
            AvaloniaProperty.Register<WalletFormLayout, ICommand>(nameof(OnExitCommand));

        public ICommand OnExitCommand
        {
            get => GetValue(OnExitCommandProperty);
            set => SetValue(OnExitCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackCommandProperty =
            AvaloniaProperty.Register<WalletFormLayout, ICommand>(nameof(OnBackCommand));

        public ICommand OnBackCommand
        {
            get => GetValue(OnBackCommandProperty);
            set => SetValue(OnBackCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnContinueCommandProperty =
            AvaloniaProperty.Register<WalletFormLayout, ICommand>(nameof(OnContinueCommand));

        public ICommand OnContinueCommand
        {
            get => GetValue(OnContinueCommandProperty);
            set => SetValue(OnContinueCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnPrimaryCommandProperty =
            AvaloniaProperty.Register<WalletFormLayout, ICommand>(nameof(OnPrimaryCommand));

        public ICommand OnPrimaryCommand
        {
            get => GetValue(OnPrimaryCommandProperty);
            set => SetValue(OnPrimaryCommandProperty, value);
        }

        public static readonly StyledProperty<object> ChildContentProperty =
            AvaloniaProperty.Register<WalletFormLayout, object>(nameof(ChildContent));

        public object ChildContent
        {
            get => GetValue(ChildContentProperty);
            set => SetValue(ChildContentProperty, value);
        }

        public WalletFormLayout()
        {
            InitializeComponent();
        }

        public WalletFormLayout(IComponentLocalizer localizer)
        {
            InitializeComponent();
            _localizer = localizer;
        }
    }

    public class WalletFormStep
    {
        public string LocalizationKey { get; set; }
        public string Icon { get; set; }
    }
}
