using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Nethereum.Wallet.UI.Components.Dashboard.Services;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;
using Nethereum.Wallet.UI.Components.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive.Linq;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Dashboard
{
    public partial class WalletDashboard : UserControl
    {
        public static readonly StyledProperty<ICommand> OnLogoutCommandProperty =
            AvaloniaProperty.Register<WalletDashboard, ICommand>(nameof(OnLogoutCommand));

        public ICommand OnLogoutCommand
        {
            get => GetValue(OnLogoutCommandProperty);
            set => SetValue(OnLogoutCommandProperty, value);
        }

        public static readonly StyledProperty<string> SelectedAccountProperty =
            AvaloniaProperty.Register<WalletDashboard, string>(nameof(SelectedAccount));

        public string SelectedAccount
        {
            get => GetValue(SelectedAccountProperty);
            set => SetValue(SelectedAccountProperty, value);
        }

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletDashboard, string>(nameof(Title), defaultValue: "Wallet Dashboard");

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> SubtitleProperty =
            AvaloniaProperty.Register<WalletDashboard, string>(nameof(Subtitle), defaultValue: "Manage your accounts, security, and wallet settings");

        public string Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly StyledProperty<string> MobileTitleProperty =
            AvaloniaProperty.Register<WalletDashboard, string>(nameof(MobileTitle), defaultValue: "Wallet");

        public string MobileTitle
        {
            get => GetValue(MobileTitleProperty);
            set => SetValue(MobileTitleProperty, value);
        }

        private readonly IDashboardPluginRegistry _pluginRegistry;
        private readonly IDashboardNavigationService _dashboardNavService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly INethereumWalletUIConfiguration _globalConfig;
        private readonly IComponentLocalizer<WalletDashboardViewModel> _localizer;
        private readonly WalletDashboardViewModel _viewModel;
        private readonly IPromptQueueService _promptQueueService;

        private IEnumerable<IDashboardPluginViewModel> AvailablePlugins => _pluginRegistry.GetAvailablePlugins();
        private IDashboardPluginViewModel? SelectedPlugin;
        private int ActivePluginIndex = 0;
        private bool drawerOpen = false;
        private int navigationCounter = 0;
        private int componentWidth = 0;
        private bool isCompact = false;

        private Dictionary<string, object>? pendingNavigationParameters;

        public ICommand HandleNotificationClickCommand { get; }

        public ICommand OnTabChangedCommand { get; }
        public ICommand SelectPluginAndCloseMenuCommand { get; }

        public WalletDashboard()
        {
            InitializeComponent();
        }

        public WalletDashboard(
            IDashboardPluginRegistry pluginRegistry,
            IDashboardNavigationService dashboardNavService,
            NethereumWalletHostProvider walletHostProvider,
            INethereumWalletUIConfiguration globalConfig,
            IComponentLocalizer<WalletDashboardViewModel> localizer,
            WalletDashboardViewModel viewModel,
            IPromptQueueService promptQueueService)
        {
            InitializeComponent();
            _pluginRegistry = pluginRegistry;
            _dashboardNavService = dashboardNavService;
            _walletHostProvider = walletHostProvider;
            _globalConfig = globalConfig;
            _localizer = localizer;
            _viewModel = viewModel;
            _promptQueueService = promptQueueService;

            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;

            HandleNotificationClickCommand = ReactiveCommand.CreateFromTask(HandleNotificationClick);
            OnTabChangedCommand = ReactiveCommand.CreateFromTask<int>(OnTabChanged);
            SelectPluginAndCloseMenuCommand = ReactiveCommand.CreateFromTask<int>(SelectPluginAndCloseMenu);
        }

        private async Task HandleNotificationClick()
        {
            await _dashboardNavService.NavigateToPluginAsync("Prompts");
        }

        private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Subscribe to events
            _walletHostProvider.SelectedAccountChanged += async (address) => await OnSelectedAccountChangedAsync(address);
            _walletHostProvider.NetworkChanged += async (chainId) => await OnNetworkChangedAsync(chainId);
            _dashboardNavService.NavigationRequested += async (sender, e) => await OnNavigationRequestedAsync(sender, e);
            _promptQueueService.QueueChanged += OnPromptQueueChanged;

            // Subscribe to ViewModel property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Initialize view model
            await _viewModel.InitializeAsync();

            // Sync with current selected account from host provider
            var currentAccount = _walletHostProvider.GetSelectedAccount();
            if (currentAccount != null)
            {
                SelectedAccount = currentAccount.Address;
            }

            var plugins = AvailablePlugins.ToList();
            if (plugins.Any())
            {
                SelectedPlugin = plugins.First();
            }

            // Initial size update
            await UpdateComponentSize();

            // Register the initial active plugin
            await RegisterActivePluginAsync();

            // Start polling for size changes (fallback if resize events don't work)
            _ = Task.Run(async () =>
            {
                while (this.IsLoaded)
                {
                    await Task.Delay(1000);
                    await Dispatcher.UIThread.InvokeAsync(UpdateComponentSize);
                }
            });
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Unsubscribe from events
            _walletHostProvider.SelectedAccountChanged -= async (address) => await OnSelectedAccountChangedAsync(address);
            _walletHostProvider.NetworkChanged -= async (chainId) => await OnNetworkChangedAsync(chainId);
            _dashboardNavService.NavigationRequested -= async (sender, e) => await OnNavigationRequestedAsync(sender, e);
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _promptQueueService.QueueChanged -= OnPromptQueueChanged;
        }

        private async Task UpdateComponentSize()
        {
            // In Avalonia, you would typically use LayoutUpdated or SizeChanged events
            // or bind to the Bounds property of the control.
            // For now, we'll simulate the JSRuntime behavior with a fixed width or actual bounds.
            // This needs to be properly implemented based on Avalonia's responsive design patterns.
            var width = Bounds.Width;

            if (Math.Abs(componentWidth - width) > 10)
            {
                var oldCompact = isCompact;
                componentWidth = (int)width;
                isCompact = width < _globalConfig.ResponsiveBreakpoint;

                if (oldCompact != isCompact)
                {
                    Console.WriteLine($"Dashboard: Responsive state changed to {(isCompact ? "compact" : "desktop")} mode (width: {componentWidth}px)");
                }

                // InvalidateVisual() to trigger re-render if needed
                InvalidateVisual();
            }
        }

        private async Task OnSelectedAccountChangedAsync(string accountAddress)
        {
            SelectedAccount = accountAddress;
            await _viewModel.RefreshSelectedAccount();
            InvalidateVisual();
        }

        private async Task OnNetworkChangedAsync(long chainId)
        {
            await _viewModel.RefreshNetworkInfo();
            InvalidateVisual();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private async Task OnTabChanged(int activeIndex)
        {
            var plugins = AvailablePlugins.ToList();
            if (activeIndex >= 0 && activeIndex < plugins.Count)
            {
                SelectedPlugin = plugins[activeIndex];
                ActivePluginIndex = activeIndex;
                navigationCounter++;
                InvalidateVisual();

                await RegisterActivePluginAsync();
            }
        }

        public string GetPluginKey(IDashboardPluginViewModel plugin)
        {
            return $"{plugin.PluginId}_{navigationCounter}";
        }

        public Type? GetPluginComponentType(IDashboardPluginViewModel plugin)
        {
            // This needs to be mapped to Avalonia UserControl types
            // For now, return null or a placeholder
            return null;
        }

        public string GetLocalizedPluginName(IDashboardPluginViewModel plugin)
        {
            return plugin.DisplayName;
        }

        public Dictionary<string, object> GetPluginParameters(IDashboardPluginViewModel plugin)
        {
            var parameters = new Dictionary<string, object>();
            var componentType = GetPluginComponentType(plugin);

            if (componentType != null)
            {
                // Use reflection to check what parameters the plugin component accepts
                var properties = componentType.GetProperties();

                if (properties.Any(p => p.Name == "SelectedAccount"))
                    parameters.Add("SelectedAccount", SelectedAccount ?? "");

                if (properties.Any(p => p.Name == "SelectedAccountAddress"))
                    parameters.Add("SelectedAccountAddress", SelectedAccount ?? "");

                if (properties.Any(p => p.Name == "ComponentWidth"))
                    parameters.Add("ComponentWidth", componentWidth);

                if (properties.Any(p => p.Name == "IsCompact"))
                    parameters.Add("IsCompact", isCompact);

                // Dashboard-specific navigation callback for create-account workflow
                if (plugin.PluginId == "create-account" && properties.Any(p => p.Name == "OnAccountAdded"))
                    parameters.Add("OnAccountAdded", ReactiveCommand.CreateFromTask(OnAccountAdded));

                // Add OnReady callback for all components
                if (properties.Any(p => p.Name == "OnReady"))
                    parameters.Add("OnReady", ReactiveCommand.Create<object>(OnPluginReady));
            }

            return parameters;
        }

        public async Task SwitchToPlugin(string pluginId)
        {
            var plugins = AvailablePlugins.ToList();
            var targetPlugin = plugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (targetPlugin != null)
            {
                var targetIndex = plugins.IndexOf(targetPlugin);
                await OnTabChanged(targetIndex);
            }
        }

        private async Task OnAccountAdded()
        {
            await SwitchToPlugin("account-list");
            InvalidateVisual();
        }

        public void ToggleDrawer()
        {
            drawerOpen = !drawerOpen;
            InvalidateVisual();
        }

        public void CloseMobileMenu()
        {
            drawerOpen = false;
            InvalidateVisual();
        }

        public void CloseDrawerIfNeeded()
        {
            drawerOpen = false;
            InvalidateVisual();
        }

        public async Task SelectPluginAndCloseMenu(int pluginIndex)
        {
            await OnTabChanged(pluginIndex);
            CloseDrawerIfNeeded();
        }

        private async Task OnNavigationRequestedAsync(object sender, Nethereum.Wallet.UI.Components.Dashboard.Services.DashboardNavigationEventArgs e)
        {
            try
            {
                pendingNavigationParameters = e.Parameters;
                await SwitchToPlugin(e.PluginId);
            }
            catch
            {
                // Handle async void exceptions
            }
        }

        private async void OnPromptQueueChanged(object? sender, PromptQueueChangedEventArgs e)
        {
            try
            {
                InvalidateVisual();

                if (e.ChangeType == PromptQueueChangeType.Added)
                {
                    await _dashboardNavService.NavigateToPluginAsync("Prompts");
                }
                else if (!_promptQueueService.HasPendingPrompts && SelectedPlugin?.PluginId == "Prompts")
                {
                    // This part needs careful handling in Avalonia to avoid UI thread issues
                    // and ensure proper state updates.
                    // For now, a direct navigation back to the first plugin.
                    var plugins = AvailablePlugins.ToList();
                    SelectedPlugin = plugins.FirstOrDefault();
                    ActivePluginIndex = SelectedPlugin != null ? plugins.IndexOf(SelectedPlugin) : 0;
                    InvalidateVisual();
                }
            }
            catch
            {
                // Suppress exceptions from async void event handler
            }
        }

        private async void OnPluginReady(object pluginInstance)
        {
            if (pendingNavigationParameters != null && pluginInstance is INavigatablePlugin nav)
            {
                await nav.NavigateWithParametersAsync(pendingNavigationParameters);
                pendingNavigationParameters = null;
            }
        }

        public string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length <= 16)
                return address;

            return $"{address.Substring(0, 8)}...{address.Substring(address.Length - 6)}";
        }

        public async Task NavigateToAccountDetails()
        {
            if (!_viewModel.HasSelectedAccount || _viewModel.SelectedAccount == null) return;

            var parameters = new Dictionary<string, object>
            {
                { "ShowAccountDetails", _viewModel.SelectedAccount },
                { "SelectedAccountAddress", _viewModel.SelectedAccountAddress }
            };

            await _dashboardNavService.NavigateToPluginAsync("account-list", parameters);
        }

        public async Task NavigateToNetworkDetails()
        {
            var parameters = new Dictionary<string, object>
            {
                { "ChainId", _viewModel.SelectedChainId },
                { "ShowNetworkDetails", true }
            };

            await _dashboardNavService.NavigateToPluginAsync("network_management", parameters);
        }

        public bool ShouldShowSidebar()
        {
            return _globalConfig.DrawerBehavior switch
            {
                DrawerBehavior.AlwaysShow => true,
                DrawerBehavior.AlwaysHidden => false,
                DrawerBehavior.Responsive => !isCompact,
                _ => !isCompact
            };
        }

        public bool ShouldShowMobileOverlay()
        {
            return _globalConfig.DrawerBehavior switch
            {
                DrawerBehavior.AlwaysShow => false,
                DrawerBehavior.AlwaysHidden => true,
                DrawerBehavior.Responsive => isCompact,
                _ => isCompact
            };
        }

        private async Task RegisterActivePluginAsync()
        {
            // This needs to be properly implemented in Avalonia to register the active plugin
            // with the DashboardNavService. It might involve finding the actual UserControl instance.
            // For now, it's a placeholder.
            // if (SelectedPlugin != null && activePluginComponentRef?.Instance != null)
            // {
            //     _dashboardNavService.RegisterActivePlugin(SelectedPlugin.PluginId, activePluginComponentRef.Instance);
            // }
        }

        public bool ShouldShowMobileHeader()
        {
            return _globalConfig.DrawerBehavior switch
            {
                DrawerBehavior.AlwaysShow => false,
                DrawerBehavior.AlwaysHidden => true,
                DrawerBehavior.Responsive => isCompact,
                _ => isCompact
            };
        }
    }
}
