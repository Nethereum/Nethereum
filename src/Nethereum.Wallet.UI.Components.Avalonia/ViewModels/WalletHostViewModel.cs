using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Services;

namespace Nethereum.Wallet.UI.Components.Avalonia.ViewModels
{
    public partial class WalletHostViewModel : ObservableObject, IDisposable
    {
        private readonly IPromptOverlayService _overlayService;

        public NethereumWalletViewModel Wallet { get; }

        [ObservableProperty]
        private PromptRequest? _currentPrompt;

        [ObservableProperty]
        private bool _isOverlayVisible;

        public WalletHostViewModel(
            NethereumWalletViewModel wallet,
            IPromptOverlayService overlayService)
        {
            Wallet = wallet;
            _overlayService = overlayService;
            _overlayService.OverlayStateChanged += OnOverlayStateChanged;

            if (_overlayService.IsOverlayVisible)
            {
                CurrentPrompt = _overlayService.CurrentPrompt;
                IsOverlayVisible = true;
            }
        }

        public async Task InitializeAsync()
        {
            await Wallet.InitializeAsync();
        }

        [RelayCommand]
        private void DismissPrompt()
        {
            _overlayService.HideOverlay();
        }

        private void OnOverlayStateChanged(object? sender, OverlayStateChangedEventArgs e)
        {
            CurrentPrompt = e.CurrentPrompt;
            IsOverlayVisible = e.IsVisible;
        }

        public void Dispose()
        {
            _overlayService.OverlayStateChanged -= OnOverlayStateChanged;
        }
    }
}
