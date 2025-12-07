using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic;
using System.Windows.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Avalonia.Views;
using System.Linq;
using ReactiveUI;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.WalletAccounts.Mnemonic
{
    public partial class MnemonicAccountCreation : UserControl
    {
        public static readonly StyledProperty<MnemonicAccountCreationViewModel> ViewModelProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, MnemonicAccountCreationViewModel>(nameof(ViewModel));

        public MnemonicAccountCreationViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly StyledProperty<MnemonicAccountEditorConfiguration> ConfigurationProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, MnemonicAccountEditorConfiguration>(nameof(Configuration), defaultValue: new MnemonicAccountEditorConfiguration());

        public MnemonicAccountEditorConfiguration Configuration
        {
            get => GetValue(ConfigurationProperty);
            set => SetValue(ConfigurationProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnAccountCreatedCommandProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, ICommand>(nameof(OnAccountCreatedCommand));

        public ICommand OnAccountCreatedCommand
        {
            get => GetValue(OnAccountCreatedCommandProperty);
            set => SetValue(OnAccountCreatedCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackToLoginCommandProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, ICommand>(nameof(OnBackToLoginCommand));

        public ICommand OnBackToLoginCommand
        {
            get => GetValue(OnBackToLoginCommandProperty);
            set => SetValue(OnBackToLoginCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnBackToAccountSelectionCommandProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, ICommand>(nameof(OnBackToAccountSelectionCommand));

        public ICommand OnBackToAccountSelectionCommand
        {
            get => GetValue(OnBackToAccountSelectionCommandProperty);
            set => SetValue(OnBackToAccountSelectionCommandProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackToLoginProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, bool>(nameof(ShowBackToLogin), defaultValue: true);

        public bool ShowBackToLogin
        {
            get => GetValue(ShowBackToLoginProperty);
            set => SetValue(ShowBackToLoginProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBackToAccountSelectionProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, bool>(nameof(ShowBackToAccountSelection), defaultValue: true);

        public bool ShowBackToAccountSelection
        {
            get => GetValue(ShowBackToAccountSelectionProperty);
            set => SetValue(ShowBackToAccountSelectionProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        public static readonly StyledProperty<int> ComponentWidthProperty =
            AvaloniaProperty.Register<MnemonicAccountCreation, int>(nameof(ComponentWidth), defaultValue: 400);

        public int ComponentWidth
        {
            get => GetValue(ComponentWidthProperty);
            set => SetValue(ComponentWidthProperty, value);
        }

        public enum FormStep
        {
            Setup = 0,
            Mnemonic = 1,
            Security = 2
        }

        public FormStep CurrentStep { get; set; } = FormStep.Setup;

        private bool isGenerateMode = true;
        private List<WalletFormStep> formSteps = new();
        private List<WalletSegmentedControlOption> modeOptions = new();

        private readonly IComponentLocalizer<MnemonicAccountCreationViewModel> _localizer;

        public ICommand GoToNextStepCommand { get; private set; }
        public ICommand GoToPreviousStepCommand { get; private set; }
        public ICommand HandleContinueCommand { get; private set; }
        public ICommand CreateAccountCommand { get; private set; }
        public ICommand BackToAccountSelectionCommand { get; private set; }
        public ICommand BackToLoginCommand { get; private set; }

        public MnemonicAccountCreation()
        {
            InitializeComponent();
        }

        public MnemonicAccountCreation(IComponentLocalizer<MnemonicAccountCreationViewModel> localizer)
        {
            InitializeComponent();
            _localizer = localizer;
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;

            GoToNextStepCommand = ReactiveCommand.Create(GoToNextStep);
            GoToPreviousStepCommand = ReactiveCommand.Create(GoToPreviousStep);
            HandleContinueCommand = ReactiveCommand.CreateFromTask(HandleContinue);
            CreateAccountCommand = ReactiveCommand.CreateFromTask(CreateAccount, this.WhenAnyValue(x => x.ViewModel.CanCreateAccount));
            BackToAccountSelectionCommand = ReactiveCommand.CreateFromTask(BackToAccountSelection);
            BackToLoginCommand = ReactiveCommand.CreateFromTask(BackToLogin);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Reset ViewModel to ensure clean state
            // ViewModel.Reset(); // Commented out - method may not exist

            if (string.IsNullOrEmpty(ViewModel.MnemonicLabel))
            {
                ViewModel.MnemonicLabel = _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.AccountNamePlaceholder);
            }

            if (string.IsNullOrEmpty(ViewModel.FinalAccountName))
            {
                ViewModel.FinalAccountName = _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.AccountNameDefaultPlaceholder);
            }

            isGenerateMode = ViewModel.IsGenerateMode;
            ViewModel.CopyToClipboardRequested += CopyToClipboard;

            SetupFormSteps();
            SetupModeOptions();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.CopyToClipboardRequested -= CopyToClipboard;
            }
        }

        private void SetupFormSteps()
        {
            formSteps = new List<WalletFormStep>
            {
                new() { LocalizationKey = MnemonicAccountEditorLocalizer.Keys.StepSetupLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("settings") },
                new() { LocalizationKey = MnemonicAccountEditorLocalizer.Keys.StepSeedPhraseLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("key") },
                new() { LocalizationKey = MnemonicAccountEditorLocalizer.Keys.StepConfirmLabel, Icon = IconMappingExtensions.ToAvaloniaPathIconData("shield") }
            };
        }

        private void SetupModeOptions()
        {
            modeOptions = new List<WalletSegmentedControlOption>
            {
                new() 
                {
                    Value = true,
                    Title = _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.GenerateTabText),
                    Description = _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.GenerateDescription),
                    Icon = IconMappingExtensions.ToAvaloniaPathIconData("auto_awesome")
                },
                new() 
                {
                    Value = false,
                    Title = _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.ImportTabText),
                    Description = _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.ImportDescription),
                    Icon = IconMappingExtensions.ToAvaloniaPathIconData("file_upload")
                }
            };
        }

        private async Task SwitchMode(bool newIsGenerateMode)
        {
            if (newIsGenerateMode != ViewModel.IsGenerateMode)
            {
                if (newIsGenerateMode)
                    await ViewModel.SwitchToGenerateModeAsync();
                else
                    await ViewModel.SwitchToImportModeAsync();
                // StateHasChanged is not needed in Avalonia, as properties are Observable
            }
        }

        private void GoToNextStep()
        {
            if (CurrentStep < FormStep.Security)
            {
                CurrentStep = (FormStep)((int)CurrentStep + 1);
                // StateHasChanged is not needed in Avalonia
            }
        }

        private void GoToPreviousStep()
        {
            if (CurrentStep > FormStep.Setup)
            {
                CurrentStep = (FormStep)((int)CurrentStep - 1);
                // StateHasChanged is not needed in Avalonia
            }
        }

        private async Task HandleContinue()
        {
            // Handle mode switching on setup step
            if (CurrentStep == FormStep.Setup && isGenerateMode != ViewModel.IsGenerateMode)
            {
                await SwitchMode(isGenerateMode);
            }
            GoToNextStep();
        }

        private bool CanProceedToNextStep()
        {
            return CurrentStep switch
            {
                FormStep.Setup => !string.IsNullOrEmpty(ViewModel.MnemonicLabel),
                FormStep.Mnemonic => ViewModel.IsGenerateMode ? !string.IsNullOrEmpty(ViewModel.Mnemonic) : ViewModel.IsValidMnemonic,
                FormStep.Security => ViewModel.CanCreateAccount && !string.IsNullOrEmpty(ViewModel.FinalAccountName),
                _ => false
            };
        }

        private string GetStepDescription()
        {
            return CurrentStep switch
            {
                FormStep.Setup => _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.Description),
                FormStep.Mnemonic => ViewModel.IsGenerateMode
                    ? _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.StepMnemonicGenerate)
                    : _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.StepMnemonicImport),
                FormStep.Security => _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.StepSecurityDescription),
                _ => ""
            };
        }

        private string GetModeTitle()
        {
            return ViewModel.IsGenerateMode
                ? _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.GenerateModeTitle)
                : _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.ImportModeTitle);
        }

        private string GetModeDescription()
        {
            return ViewModel.IsGenerateMode
                ? _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.GenerateModeDescription)
                : _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.ImportModeDescription);
        }

        private List<string> GetMnemonicWordsList()
        {
            if (string.IsNullOrEmpty(ViewModel.Mnemonic))
                return new List<string>();

            return ViewModel.Mnemonic.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private async Task CreateAccount()
        {
            if (ViewModel.CanCreateAccount)
            {
                OnAccountCreatedCommand?.Execute(null);
            }
        }

        private async Task CopyToClipboard(string text)
        {
            if (!Configuration.MnemonicBehavior.EnableClipboardCopy)
                return;

            // Avalonia doesn't have a direct equivalent of JSRuntime for clipboard access
            // You would typically use Avalonia.Application.Current.Clipboard.SetTextAsync(text);
            // However, this requires a reference to Avalonia.Application, which might not be available in a UserControl
            // For now, I'll leave this as a placeholder.
            // await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }

        private async Task BackToLogin()
        {
            if (OnBackToLoginCommand?.CanExecute(null) == true)
            {
                OnBackToLoginCommand.Execute(null);
            }
        }

        private async Task BackToAccountSelection()
        {
            if (OnBackToAccountSelectionCommand?.CanExecute(null) == true)
            {
                OnBackToAccountSelectionCommand.Execute(null);
            }
        }
    }

    public class WalletSegmentedControlOption
    {
        public bool Value { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}
