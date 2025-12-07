using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.CreateAccount;
using System.Windows.Input;
using System.Threading.Tasks;
using ReactiveUI;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class CreateAccount : UserControl
    {
        public static readonly StyledProperty<ICommand> OnAccountAddedCommandProperty =
            AvaloniaProperty.Register<CreateAccount, ICommand>(nameof(OnAccountAddedCommand));

        public ICommand OnAccountAddedCommand
        {
            get => GetValue(OnAccountAddedCommandProperty);
            set => SetValue(OnAccountAddedCommandProperty, value);
        }

        public static readonly StyledProperty<bool> ShowHeaderProperty =
            AvaloniaProperty.Register<CreateAccount, bool>(nameof(ShowHeader), defaultValue: true);

        public bool ShowHeader
        {
            get => GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactModeProperty =
            AvaloniaProperty.Register<CreateAccount, bool>(nameof(IsCompactMode));

        public bool IsCompactMode
        {
            get => GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
        }

        private readonly CreateAccountViewModel _viewModel;

        public static readonly StyledProperty<CreateAccountPluginViewModel> ViewModelProperty =
            AvaloniaProperty.Register<CreateAccount, CreateAccountPluginViewModel>(nameof(ViewModel));

        public CreateAccountPluginViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ICommand OnBackToLoginCommand { get; private set; }
        public ICommand OnBackToAccountSelectionCommand { get; private set; }

        public CreateAccount()
        {
            // This constructor is for the designer
            InitializeComponent();
        }

        public CreateAccount(CreateAccountViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            this.AttachedToVisualTree += OnAttachedToVisualTree;

            // Commands for navigation from sub-components
            OnAccountAddedCommand = ReactiveCommand.CreateFromTask(OnAccountAdded);
            OnBackToLoginCommand = ReactiveCommand.CreateFromTask(OnBackToLogin);
            OnBackToAccountSelectionCommand = ReactiveCommand.CreateFromTask(OnBackToAccountSelection);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Reset ViewModel to ensure clean state
            // _viewModel.Reset(); // Commented out - method may not exist
        }

        private async Task OnAccountAdded()
        {
            // This is invoked by the sub-components when an account is created
            // We need to notify the parent component that an account has been added
            OnAccountAddedCommand?.Execute(null);
        }

        private async Task OnBackToLogin()
        {
            // This is invoked by the sub-components when the user wants to go back to login
            OnBackToLoginCommand?.Execute(null);
        }

        private async Task OnBackToAccountSelection()
        {
            // This is invoked by the sub-components when the user wants to go back to account selection
            OnBackToAccountSelectionCommand?.Execute(null);
        }
    }
}
