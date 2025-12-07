using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey;
using System.Windows.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Avalonia.Views;
using System.Linq;
using ReactiveUI;
using System.Reactive.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.WalletAccounts.PrivateKey
{
    public partial class PrivateKeyAccountCreation : UserControl
    {
        public static readonly StyledProperty<PrivateKeyAccountCreationViewModel> ViewModelProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, PrivateKeyAccountCreationViewModel>(nameof(ViewModel));

        public PrivateKeyAccountCreationViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnAccountCreatedCommandProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, ICommand>(nameof(OnAccountCreatedCommand));

        public ICommand OnAccountCreatedCommand
        {
            get => GetValue(OnAccountCreatedCommandProperty);
            set => SetValue(OnAccountCreatedCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackToLoginCommandProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, ICommand>(nameof(OnBackToLoginCommand));

        public ICommand OnBackToLoginCommand
        {
            get => GetValue(OnBackToLoginCommandProperty);
            set => SetValue(OnBackToLoginCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackToAccountSelectionCommandProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, ICommand>(nameof(OnBackToAccountSelectionCommand));

        public ICommand OnBackToAccountSelectionCommand
        {
            get => GetValue(OnBackToAccountSelectionCommandProperty);
            set => SetValue(OnBackToAccountSelectionCommandProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackToLoginProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, bool>(nameof(ShowBackToLogin), defaultValue: true);

        public bool ShowBackToLogin
        {
            get => GetValue(ShowBackToLoginProperty);
            set => SetValue(ShowBackToLoginProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackToAccountSelectionProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, bool>(nameof(ShowBackToAccountSelection), defaultValue: true);

        public bool ShowBackToAccountSelection
        {
            get => GetValue(ShowBackToAccountSelectionProperty);
            set => SetValue(ShowBackToAccountSelectionProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public static readonly StyledProperty<int> ComponentWidthProperty =
            AvaloniaProperty.Register<PrivateKeyAccountCreation, int>(nameof(ComponentWidth), defaultValue: 400);

        public int ComponentWidth
        {
            get => GetValue(ComponentWidthProperty);
            set => SetValue(ComponentWidthProperty, value);
        }

        public enum FormStep
        {
            Setup = 0,
            PrivateKey = 1,
            Confirm = 2
        }

        public FormStep CurrentStep { get; set; } = FormStep.Setup;

        private bool confirmBackup = false;
        private List<WalletFormStep> formSteps = new();

        private readonly IComponentLocalizer<PrivateKeyAccountCreationViewModel> _localizer;

        public ICommand HandleContinueCommand { get; }
        public ICommand HandleBackCommand { get; }
        public ICommand HandleExitCommand { get; }
        public ICommand CreateAccountCommand { get; }
        public ICommand HandleAddressCopiedCommand { get; }

        public PrivateKeyAccountCreation()
        {
            InitializeComponent();
        }

        public PrivateKeyAccountCreation(IComponentLocalizer<PrivateKeyAccountCreationViewModel> localizer)
        {
            InitializeComponent();
            _localizer = localizer;
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;

            HandleContinueCommand = ReactiveCommand.Create(HandleContinue, this.WhenAnyValue(x => x.CurrentStep, x => x.ViewModel.IsValidPrivateKey, x => x.ViewModel.PrivateKey, x => x.confirmBackup, x => x.ViewModel.CanCreateAccount).Select(_ => CanContinue()));
            HandleBackCommand = ReactiveCommand.Create(HandleBack);
            HandleExitCommand = ReactiveCommand.CreateFromTask(HandleExit);
            CreateAccountCommand = ReactiveCommand.CreateFromTask(CreateAccount, this.WhenAnyValue(x => x.ViewModel.CanCreateAccount, x => x.confirmBackup).Select(x => x.Item1 && x.Item2));
            HandleAddressCopiedCommand = ReactiveCommand.CreateFromTask<string>(HandleAddressCopied);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Reset ViewModel to ensure clean state
            ViewModel.Reset();

            if (string.IsNullOrEmpty(ViewModel.Label))
            {
                ViewModel.Label = "";
            }

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
                new() { LocalizationKey = PrivateKeyAccountEditorLocalizer.Keys.StepSetupLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("settings") },
                new() { LocalizationKey = PrivateKeyAccountEditorLocalizer.Keys.StepPrivateKeyLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("key") },
                new() { LocalizationKey = PrivateKeyAccountEditorLocalizer.Keys.StepConfirmLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("shield") }
            };
        }

        private string GetTitle()
        {
            return CurrentStep switch
            {
                FormStep.Setup => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.CreatePrivateKeyAccountTitle),
                FormStep.PrivateKey => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.EnterPrivateKeyTitle),
                FormStep.Confirm => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.ConfirmAccountTitle),
                _ => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.CreatePrivateKeyAccountTitle)
            };
        }

        private string GetSubtitle()
        {
            return CurrentStep switch
            {
                FormStep.Setup => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.SetupAccountSubtitle),
                FormStep.PrivateKey => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.PrivateKeySubtitle),
                FormStep.Confirm => _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.ConfirmSubtitle),
                _ => ""
            };
        }

        private string GetExitText()
        {
            if (ShowBackToLogin)
                return _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.BackToLoginText);
            if (ShowBackToAccountSelection)
                return _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.BackToAccountSelectionText);
            return _localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.ExitButtonText);
        }

        private bool CanContinue()
        {
            return CurrentStep switch
            {
                FormStep.Setup => true, // Account name is optional
                FormStep.PrivateKey => ViewModel.IsValidPrivateKey && !string.IsNullOrEmpty(ViewModel.PrivateKey),
                FormStep.Confirm => confirmBackup && ViewModel.CanCreateAccount,
                _ => false
            };
        }

        private void HandleContinue()
        {
            if (CanContinue() && CurrentStep < FormStep.Confirm)
            {
                CurrentStep++;
                // StateHasChanged is not needed in Avalonia
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

        private async Task CreateAccount()
        {
            if (ViewModel.CanCreateAccount && confirmBackup)
            {
                OnAccountCreatedCommand?.Execute(null);
            }
        }

        private async Task HandleAddressCopied(string address)
        {
            // Snackbar.Add(Localizer.GetString(Keys.CopiedToClipboard), Severity.Success);
            // Need to implement a notification service for Avalonia
        }

        private string GetSecurityWarningMessage()
        {
            return $"{_localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.NeverShareAdvice)} {_localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.SecureEnvironmentAdvice)} {_localizer.GetString(PrivateKeyAccountEditorLocalizer.Keys.FullControlAdvice)}";
        }
    }
}
