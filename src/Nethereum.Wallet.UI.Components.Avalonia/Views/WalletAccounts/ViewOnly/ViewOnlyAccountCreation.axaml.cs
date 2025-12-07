using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly;
using System.Windows.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Avalonia.Views;
using System.Linq;
using ReactiveUI;
using System.Reactive.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.WalletAccounts.ViewOnly
{
    public partial class ViewOnlyAccountCreation : UserControl
    {
        public static readonly StyledProperty<ViewOnlyAccountCreationViewModel> ViewModelProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, ViewOnlyAccountCreationViewModel>(nameof(ViewModel));

        public ViewOnlyAccountCreationViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnAccountCreatedCommandProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, ICommand>(nameof(OnAccountCreatedCommand));

        public ICommand OnAccountCreatedCommand
        {
            get => GetValue(OnAccountCreatedCommandProperty);
            set => SetValue(OnAccountCreatedCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackToLoginCommandProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, ICommand>(nameof(OnBackToLoginCommand));

        public ICommand OnBackToLoginCommand
        {
            get => GetValue(OnBackToLoginCommandProperty);
            set => SetValue(OnBackToLoginCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackToAccountSelectionCommandProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, ICommand>(nameof(OnBackToAccountSelectionCommand));

        public ICommand OnBackToAccountSelectionCommand
        {
            get => GetValue(OnBackToAccountSelectionCommandProperty);
            set => SetValue(OnBackToAccountSelectionCommandProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackToLoginProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, bool>(nameof(ShowBackToLogin), defaultValue: true);

        public bool ShowBackToLogin
        {
            get => GetValue(ShowBackToLoginProperty);
            set => SetValue(ShowBackToLoginProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackToAccountSelectionProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, bool>(nameof(ShowBackToAccountSelection), defaultValue: true);

        public bool ShowBackToAccountSelection
        {
            get => GetValue(ShowBackToAccountSelectionProperty);
            set => SetValue(ShowBackToAccountSelectionProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<ViewOnlyAccountCreation, bool>(nameof(IsCompactMode), defaultValue: false);

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public enum FormStep
        {
            Setup = 0,
            Address = 1,
            Confirm = 2
        }

        public FormStep CurrentStep { get; set; } = FormStep.Setup;

        private List<WalletFormStep> formSteps = new();

        private readonly IComponentLocalizer<ViewOnlyAccountCreationViewModel> _localizer;

        public IComponentLocalizer<ViewOnlyAccountCreationViewModel> Localizer => _localizer;

        public bool ShowBack => CurrentStep > FormStep.Setup;
        public bool ShowContinue => CurrentStep < FormStep.Confirm;
        public bool ShowPrimary => CurrentStep == FormStep.Confirm;

        public ICommand HandleContinueCommand { get; }
        public ICommand HandleBackCommand { get; }
        public ICommand HandleExitCommand { get; }
        public ICommand CreateAccountCommand { get; }

        public ViewOnlyAccountCreation()
        {
            InitializeComponent();
        }

        public ViewOnlyAccountCreation(IComponentLocalizer<ViewOnlyAccountCreationViewModel> localizer)
        {
            InitializeComponent();
            _localizer = localizer;
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;

            HandleContinueCommand = ReactiveCommand.Create(HandleContinue, this.WhenAnyValue(x => x.CurrentStep, x => x.ViewModel.ViewOnlyAddress).Select(_ => CanContinue()));
            HandleBackCommand = ReactiveCommand.Create(HandleBack);
            HandleExitCommand = ReactiveCommand.CreateFromTask(HandleExit);
            CreateAccountCommand = ReactiveCommand.CreateFromTask(CreateAccount, this.WhenAnyValue(x => x.ViewModel.CanCreateAccount).Select(x => x));
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Reset ViewModel to ensure clean state
            ViewModel.Reset();

            SetupFormSteps();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Clean up subscriptions if any
        }

        private void SetupFormSteps()
        {
            formSteps = new List<WalletFormStep>
            {
                new() { LocalizationKey = ViewOnlyAccountEditorLocalizer.Keys.StepSetupLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("settings") },
                new() { LocalizationKey = ViewOnlyAccountEditorLocalizer.Keys.StepAddressLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("account_box") },
                new() { LocalizationKey = ViewOnlyAccountEditorLocalizer.Keys.StepConfirmLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("check_circle") }
            };
        }

        private string GetTitle()
        {
            return CurrentStep switch
            {
                FormStep.Setup => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.SetupAccountTitle),
                FormStep.Address => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.EnterAddressTitle),
                FormStep.Confirm => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.ConfirmDetailsTitle),
                _ => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.AddViewOnlyAccount)
            };
        }

        private string GetSubtitle()
        {
            return CurrentStep switch
            {
                FormStep.Setup => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.SetupAccountSubtitle),
                FormStep.Address => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.EnterAddressSubtitle),
                FormStep.Confirm => _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.ConfirmDetailsSubtitle),
                _ => ""
            };
        }

        private string GetExitText()
        {
            if (ShowBackToLogin)
                return _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.BackToLoginText);
            if (ShowBackToAccountSelection)
                return _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.BackToAccountSelectionText);
            return _localizer.GetString(ViewOnlyAccountEditorLocalizer.Keys.Exit);
        }

        private bool CanContinue()
        {
            return CurrentStep switch
            {
                FormStep.Setup => true, // Name is optional
                FormStep.Address => IsValidAddress(),
                _ => false
            };
        }

        private bool IsValidAddress()
        {
            return !string.IsNullOrWhiteSpace(ViewModel.ViewOnlyAddress) &&
                   ViewModel.ViewOnlyAddress.StartsWith("0x") &&
                   ViewModel.ViewOnlyAddress.Length == 42;
        }

        private async Task HandleExit()
        {
            if (ShowBackToLogin && OnBackToLoginCommand?.CanExecute(null) == true)
            {
                OnBackToLoginCommand.Execute(null);
            }
            else if (ShowBackToAccountSelection && OnBackToAccountSelectionCommand?.CanExecute(null) == true)
            {
                OnBackToAccountSelectionCommand.Execute(null);
            }
        }

        private void HandleBack()
        {
            if (CurrentStep > FormStep.Setup)
            {
                CurrentStep--;
                // StateHasChanged is not needed in Avalonia
            }
        }

        private void HandleContinue()
        {
            if (CanContinue() && CurrentStep < FormStep.Confirm)
            {
                CurrentStep++;
                // StateHasChanged is not needed in Avalonia
            }
        }

        private async Task CreateAccount()
        {
            if (ViewModel.CanCreateAccount)
            {
                OnAccountCreatedCommand?.Execute(null);
            }
        }
    }
}
