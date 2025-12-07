using Nethereum.UI;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Windows.Input;
using System.Threading.Tasks;
using ReactiveUI;
using System.Reactive.Linq;
using System.Collections.Generic;
using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class NethereumWallet : UserControl, INotifyPropertyChanged
    {
        public static readonly StyledProperty<ICommand> OnConnectedCommandProperty =
            AvaloniaProperty.Register<NethereumWallet, ICommand>(nameof(OnConnectedCommand));

        public ICommand OnConnectedCommand
        {
            get => GetValue(OnConnectedCommandProperty);
            set => SetValue(OnConnectedCommandProperty, value);
        }

        public static readonly StyledProperty<string> WidthProperty =
            AvaloniaProperty.Register<NethereumWallet, string>(nameof(Width));

        public string Width
        {
            get => GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public static readonly StyledProperty<string> HeightProperty =
            AvaloniaProperty.Register<NethereumWallet, string>(nameof(Height));

        public string Height
        {
            get => GetValue(HeightProperty);
            set => SetValue(HeightProperty, value);
        }

        public static readonly StyledProperty<DrawerBehavior?> DrawerBehaviorProperty =
            AvaloniaProperty.Register<NethereumWallet, DrawerBehavior?>(nameof(DrawerBehavior));

        public DrawerBehavior? DrawerBehavior
        {
            get => GetValue(DrawerBehaviorProperty);
            set => SetValue(DrawerBehaviorProperty, value);
        }

        public static readonly StyledProperty<int?> ResponsiveBreakpointProperty =
            AvaloniaProperty.Register<NethereumWallet, int?>(nameof(ResponsiveBreakpoint));

        public int? ResponsiveBreakpoint
        {
            get => GetValue(ResponsiveBreakpointProperty);
            set => SetValue(ResponsiveBreakpointProperty, value);
        }

        public static readonly StyledProperty<int?> SidebarWidthProperty =
            AvaloniaProperty.Register<NethereumWallet, int?>(nameof(SidebarWidth));

        public int? SidebarWidth
        {
            get => GetValue(SidebarWidthProperty);
            set => SetValue(SidebarWidthProperty, value);
        }

        public static readonly StyledProperty<bool?> ShowLogoProperty =
            AvaloniaProperty.Register<NethereumWallet, bool?>(nameof(ShowLogo));

        public bool? ShowLogo
        {
            get => GetValue(ShowLogoProperty);
            set => SetValue(ShowLogoProperty, value);
        }

        public static readonly StyledProperty<bool?> ShowApplicationNameProperty =
            AvaloniaProperty.Register<NethereumWallet, bool?>(nameof(ShowApplicationName));

        public bool? ShowApplicationName
        {
            get => GetValue(ShowApplicationNameProperty);
            set => SetValue(ShowApplicationNameProperty, value);
        }

        public static readonly StyledProperty<bool?> ShowNetworkInHeaderProperty =
            AvaloniaProperty.Register<NethereumWallet, bool?>(nameof(ShowNetworkInHeader));

        public bool? ShowNetworkInHeader
        {
            get => GetValue(ShowNetworkInHeaderProperty);
            set => SetValue(ShowNetworkInHeaderProperty, value);
        }

        public static readonly StyledProperty<bool?> ShowAccountDetailsInHeaderProperty =
            AvaloniaProperty.Register<NethereumWallet, bool?>(nameof(ShowAccountDetailsInHeader));

        public bool? ShowAccountDetailsInHeader
        {
            get => GetValue(ShowAccountDetailsInHeaderProperty);
            set => SetValue(ShowAccountDetailsInHeaderProperty, value);
        }

        private readonly NethereumWalletViewModel _viewModel;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly SelectedEthereumHostProviderService _selectedHostProviderService;
        private readonly IWalletVaultService _vaultService;
        private readonly INethereumWalletUIConfiguration _globalConfig;
        private readonly NethereumWalletConfiguration _config;
        private readonly IComponentLocalizer<NethereumWalletViewModel> _localizer;

        public event PropertyChangedEventHandler? PropertyChanged;

        public IComponentLocalizer<NethereumWalletViewModel> Localizer => _localizer;
        public INethereumWalletUIConfiguration GlobalConfig => _globalConfig;
        public NethereumWalletConfiguration Config => _config;

        // Localized string properties for XAML binding
        public string CreateTitle => _localizer?.GetString(NethereumWalletLocalizer.Keys.CreateTitle) ?? "Create Wallet";
        public string CreateSubtitle => _localizer?.GetString(NethereumWalletLocalizer.Keys.CreateSubtitle) ?? "Set up your new wallet";
        public string LoginTitle => _localizer?.GetString(NethereumWalletLocalizer.Keys.LoginTitle) ?? "Login";
        public string LoginSubtitle => _localizer?.GetString(NethereumWalletLocalizer.Keys.LoginSubtitle) ?? "Enter your password to unlock your wallet";
        public string PasswordLabel => _localizer?.GetString(NethereumWalletLocalizer.Keys.PasswordLabel) ?? "Password";
        public string CreatePasswordLabel => _localizer?.GetString(NethereumWalletLocalizer.Keys.CreatePasswordLabel) ?? "Create Password";
        public string ConfirmPasswordLabel => _localizer?.GetString(NethereumWalletLocalizer.Keys.ConfirmPasswordLabel) ?? "Confirm Password";
        public string CreateNewWalletLinkText => _localizer?.GetString(NethereumWalletLocalizer.Keys.CreateNewWalletLinkText) ?? "Create New Wallet";
        public string LoginButton => _localizer?.GetString(NethereumWalletLocalizer.Keys.LoginButton) ?? "Login";
        public string ResetWallet => _localizer?.GetString(NethereumWalletLocalizer.Keys.ResetWallet) ?? "Reset Wallet";
        public string PasswordHelperText => _localizer?.GetString(NethereumWalletLocalizer.Keys.PasswordHelperText) ?? "Password requirements";
        public string PasswordMismatch => _localizer?.GetString(NethereumWalletLocalizer.Keys.PasswordMismatch) ?? "Passwords do not match";
        public string CreateButtonText => _localizer?.GetString(NethereumWalletLocalizer.Keys.CreateButtonText) ?? "Create Wallet";

        private bool showPassword = false;
        private bool showNewPassword = false;
        private bool showConfirmPassword = false;

        public bool ShowPassword => showPassword;
        public bool ShowNewPassword => showNewPassword;
        public bool ShowConfirmPassword => showConfirmPassword;

        public bool PasswordsMatch => _viewModel?.NewPassword == _viewModel?.ConfirmPassword;

        public ICommand ShowCreateNewWalletCommand { get; private set; }
        public ICommand OnLogoutCommand { get; private set; }
        public ICommand TogglePasswordVisibilityCommand { get; private set; }
        public ICommand ToggleNewPasswordVisibilityCommand { get; private set; }
        public ICommand ToggleConfirmPasswordVisibilityCommand { get; private set; }
        public ICommand OnKeyDownCommand { get; private set; }

        public NethereumWallet()
        {
            InitializeComponent();
        }

        public static NethereumWallet Create(IServiceProvider serviceProvider)
        {
            var viewModel = serviceProvider.GetRequiredService<NethereumWalletViewModel>();
            var walletHostProvider = serviceProvider.GetRequiredService<NethereumWalletHostProvider>();
            var selectedHostProviderService = serviceProvider.GetRequiredService<SelectedEthereumHostProviderService>();
            var vaultService = serviceProvider.GetRequiredService<IWalletVaultService>();
            var globalConfig = serviceProvider.GetRequiredService<INethereumWalletUIConfiguration>();
            var config = serviceProvider.GetRequiredService<NethereumWalletConfiguration>();
            var localizer = serviceProvider.GetRequiredService<IComponentLocalizer<NethereumWalletViewModel>>();

            return new NethereumWallet(viewModel, walletHostProvider, selectedHostProviderService, vaultService, globalConfig, config, localizer);
        }

        public NethereumWallet(
            NethereumWalletViewModel viewModel,
            NethereumWalletHostProvider walletHostProvider,
            SelectedEthereumHostProviderService selectedHostProviderService,
            IWalletVaultService vaultService,
            INethereumWalletUIConfiguration globalConfig,
            NethereumWalletConfiguration config,
            IComponentLocalizer<NethereumWalletViewModel> localizer)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _walletHostProvider = walletHostProvider;
            _selectedHostProviderService = selectedHostProviderService;
            _vaultService = vaultService;
            _globalConfig = globalConfig;
            _config = config;
            _localizer = localizer;

            InitializeDependencies();
        }

        private void InitializeDependencies()
        {
            if (_viewModel == null) return;

            DataContext = _viewModel;

            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;

            ShowCreateNewWalletCommand = ReactiveCommand.Create(ShowCreateNewWallet);
            OnLogoutCommand = ReactiveCommand.CreateFromTask(OnLogout);
            TogglePasswordVisibilityCommand = ReactiveCommand.Create(TogglePasswordVisibility);
            ToggleNewPasswordVisibilityCommand = ReactiveCommand.Create(ToggleNewPasswordVisibility);
            ToggleConfirmPasswordVisibilityCommand = ReactiveCommand.Create(ToggleConfirmPasswordVisibility);
            OnKeyDownCommand = ReactiveCommand.Create<KeyEventArgs>(OnKeyDown);
        }

        private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_viewModel == null) return;

            ApplyParameterOverrides();
            await _viewModel.InitializeAsync();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _viewModel.OnWalletConnected = async () =>
            {
                if (OnConnectedCommand?.CanExecute(null) == true)
                {
                    OnConnectedCommand.Execute(null);
                }
            };
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
        }

        private void ApplyParameterOverrides()
        {
            if (_globalConfig == null) return;

            if (DrawerBehavior.HasValue)
                _globalConfig.DrawerBehavior = DrawerBehavior.Value;
            if (ResponsiveBreakpoint.HasValue)
                _globalConfig.ResponsiveBreakpoint = ResponsiveBreakpoint.Value;
            if (SidebarWidth.HasValue)
                _globalConfig.SidebarWidth = SidebarWidth.Value;
            if (ShowLogo.HasValue)
                _globalConfig.ShowLogo = ShowLogo.Value;
            if (ShowApplicationName.HasValue)
                _globalConfig.ShowApplicationName = ShowApplicationName.Value;
            if (ShowNetworkInHeader.HasValue)
                _globalConfig.ShowNetworkInHeader = ShowNetworkInHeader.Value;
            if (ShowAccountDetailsInHeader.HasValue)
                _globalConfig.ShowAccountDetailsInHeader = ShowAccountDetailsInHeader.Value;
        }

        private string GetContainerStyle()
        {
            var styles = new List<string> { "height: 100%", "width: 100%" };

            if (!string.IsNullOrEmpty(Width))
                styles[1] = $"width: {Width}";
            if (!string.IsNullOrEmpty(Height))
                styles[0] = $"height: {Height}";

            return string.Join("; ", styles);
        }

        private async Task OnAccountAdded()
        {
            if (_viewModel == null) return;

            await _viewModel.CheckAccountsAsync();

            if (_viewModel.HasAccounts)
            {
                await _walletHostProvider.EnableProviderAsync();
                await _selectedHostProviderService.SetSelectedEthereumHostProvider(_walletHostProvider);
            }

            // InvalidateVisual() to trigger re-render if needed
            InvalidateVisual();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.NewPassword) || e.PropertyName == nameof(_viewModel.ConfirmPassword))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PasswordsMatch)));
            }
            InvalidateVisual();
        }

        private async void OnKeyDown(KeyEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.Key == Key.Enter && !string.IsNullOrEmpty(_viewModel.Password))
            {
                _viewModel.LoginCommand.Execute(null);
            }
        }

        private void TogglePasswordVisibility()
        {
            showPassword = !showPassword;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPassword)));
        }

        private void ToggleNewPasswordVisibility()
        {
            showNewPassword = !showNewPassword;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowNewPassword)));
        }

        private void ToggleConfirmPasswordVisibility()
        {
            showConfirmPassword = !showConfirmPassword;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowConfirmPassword)));
        }

        private void ShowCreateNewWallet()
        {
            if (_viewModel == null) return;

            _viewModel.VaultExists = false;
            InvalidateVisual();
        }

        private async Task OnLogout()
        {
            if (_viewModel == null) return;

            _viewModel.LogoutCommand.Execute(null);
            InvalidateVisual();
        }

        // Helper to convert string color to Avalonia Color
        private Color GetAvaloniaColor(string colorString)
        {
            return colorString?.ToLowerInvariant() switch
            {
                "error" => Color.FromRgb(244, 67, 54), // MudBlazor Error
                "warning" => Color.FromRgb(255, 152, 0), // MudBlazor Warning
                "success" => Color.FromRgb(76, 175, 80), // MudBlazor Success
                _ => Color.FromRgb(0, 0, 0) // Default to black
            };
        }
    }
}
