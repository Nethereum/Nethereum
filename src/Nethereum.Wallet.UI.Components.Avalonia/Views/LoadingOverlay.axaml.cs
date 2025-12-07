using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Avalonia.Services;
using Avalonia.Data;
using Avalonia.Threading;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public partial class LoadingOverlay : UserControl
    {
        private readonly IWalletLoadingService _loadingService;

        public static readonly DirectProperty<LoadingOverlay, bool> IsLoadingProperty =
            AvaloniaProperty.RegisterDirect<LoadingOverlay, bool>(
                nameof(IsLoading),
                o => o.IsLoading,
                (o, v) => o.IsLoading = v);

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
        }

        public static readonly DirectProperty<LoadingOverlay, double> ProgressProperty =
            AvaloniaProperty.RegisterDirect<LoadingOverlay, double>(
                nameof(Progress),
                o => o.Progress,
                (o, v) => o.Progress = v);

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetAndRaise(ProgressProperty, ref _progress, value);
        }

        public static readonly DirectProperty<LoadingOverlay, string> LoadingMessageProperty =
            AvaloniaProperty.RegisterDirect<LoadingOverlay, string>(
                nameof(LoadingMessage),
                o => o.LoadingMessage,
                (o, v) => o.LoadingMessage = v);

        private string _loadingMessage;
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetAndRaise(LoadingMessageProperty, ref _loadingMessage, value);
        }

        public LoadingOverlay()
        {
            InitializeComponent();
            // This is for the designer
        }

        public LoadingOverlay(IWalletLoadingService loadingService)
        {
            InitializeComponent();
            _loadingService = loadingService;
            if (_loadingService is INotifyWalletLoadingService notifyWalletLoadingService)
            {
                notifyWalletLoadingService.LoadingStateChanged += OnLoadingStateChanged;
                notifyWalletLoadingService.ProgressChanged += OnProgressChanged;
            }
        }

        private void OnLoadingStateChanged(bool isLoading, string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = isLoading;
                LoadingMessage = message;
            });
        }

        private void OnProgressChanged(double progress, string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Progress = progress;
                if (!string.IsNullOrEmpty(message))
                {
                    LoadingMessage = message;
                }
            });
        }

        public void Dispose()
        {
            if (_loadingService is INotifyWalletLoadingService notifyWalletLoadingService)
            {
                notifyWalletLoadingService.LoadingStateChanged -= OnLoadingStateChanged;
                notifyWalletLoadingService.ProgressChanged -= OnProgressChanged;
            }
        }
    }
}
