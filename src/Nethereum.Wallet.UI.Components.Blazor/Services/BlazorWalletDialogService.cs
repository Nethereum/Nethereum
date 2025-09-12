using System;
using System.Threading.Tasks;
using MudBlazor;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Blazor.Shared;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class BlazorWalletDialogService : IWalletDialogService
    {
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;

        public BlazorWalletDialogService(IDialogService dialogService, IServiceProvider serviceProvider)
        {
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var parameters = new DialogParameters<WalletPromptDialog>
            {
                { x => x.Title, title },
                { x => x.Message, message },
                { x => x.Icon, Icons.Material.Filled.HelpOutline },
                { x => x.IconColor, Color.Warning },
                { x => x.ConfirmText, "Confirm" },
                { x => x.CancelText, "Cancel" },
                { x => x.ConfirmColor, Color.Primary },
                { x => x.ShowCancel, true }
            };

            var options = new DialogOptions
            {
                BackdropClick = false,
                MaxWidth = MaxWidth.Small,
                CloseButton = false
            };

            var dialog = await _dialogService.ShowAsync<WalletPromptDialog>("", parameters, options);
            var result = await dialog.Result;

            return result != null && !result.Canceled;
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            var parameters = new DialogParameters<WalletPromptDialog>
            {
                { x => x.Title, title },
                { x => x.Message, message },
                { x => x.Icon, Icons.Material.Filled.Info },
                { x => x.IconColor, Color.Info },
                { x => x.ConfirmText, "OK" },
                { x => x.ConfirmColor, Color.Primary },
                { x => x.ShowCancel, false }
            };

            var options = new DialogOptions
            {
                BackdropClick = false,
                MaxWidth = MaxWidth.Small,
                CloseButton = false
            };

            var dialog = await _dialogService.ShowAsync<WalletPromptDialog>("", parameters, options);
            await dialog.Result;
        }

        public async Task<T?> ShowDialogAsync<T>(object? parameters = null) where T : class
        {
            return null;
        }
        public async Task<bool> ShowWarningConfirmationAsync(string title, string message, string confirmText = "Remove", string cancelText = "Cancel")
        {
            var parameters = new DialogParameters<WalletPromptDialog>
            {
                { x => x.Title, title },
                { x => x.Message, message },
                { x => x.Icon, Icons.Material.Filled.Warning },
                { x => x.IconColor, Color.Warning },
                { x => x.ConfirmText, confirmText },
                { x => x.CancelText, cancelText },
                { x => x.ConfirmColor, Color.Error },
                { x => x.ShowCancel, true }
            };

            var options = new DialogOptions
            {
                BackdropClick = false,
                MaxWidth = MaxWidth.Small,
                CloseButton = false
            };

            var dialog = await _dialogService.ShowAsync<WalletPromptDialog>("", parameters, options);
            var result = await dialog.Result;

            return result != null && !result.Canceled;
        }
        public async Task ShowErrorAsync(string title, string message)
        {
            var parameters = new DialogParameters<WalletPromptDialog>
            {
                { x => x.Title, title },
                { x => x.Message, message },
                { x => x.Icon, Icons.Material.Filled.Error },
                { x => x.IconColor, Color.Error },
                { x => x.ConfirmText, "OK" },
                { x => x.ConfirmColor, Color.Primary },
                { x => x.ShowCancel, false }
            };

            var options = new DialogOptions
            {
                BackdropClick = false,
                MaxWidth = MaxWidth.Small,
                CloseButton = false
            };

            var dialog = await _dialogService.ShowAsync<WalletPromptDialog>("", parameters, options);
            await dialog.Result;
        }
        public async Task ShowSuccessAsync(string title, string message)
        {
            var parameters = new DialogParameters<WalletPromptDialog>
            {
                { x => x.Title, title },
                { x => x.Message, message },
                { x => x.Icon, Icons.Material.Filled.CheckCircle },
                { x => x.IconColor, Color.Success },
                { x => x.ConfirmText, "OK" },
                { x => x.ConfirmColor, Color.Primary },
                { x => x.ShowCancel, false }
            };

            var options = new DialogOptions
            {
                BackdropClick = false,
                MaxWidth = MaxWidth.Small,
                CloseButton = false
            };

            var dialog = await _dialogService.ShowAsync<WalletPromptDialog>("", parameters, options);
            await dialog.Result;
        }
    }
}