using System;
using System.Threading.Tasks;
using Nethereum.Wallet.UI.Components.Abstractions;

namespace Nethereum.Wallet.UI.Components.Avalonia.Services
{
    public class AvaloniaWalletDialogService : IWalletDialogService
    {
        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            return await Task.FromResult(true);
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            await Task.CompletedTask;
        }

        public async Task<T?> ShowDialogAsync<T>(object? parameters = null) where T : class
        {
            return await Task.FromResult<T?>(null);
        }

        public async Task<bool> ShowWarningConfirmationAsync(string title, string message, string confirmText = "Remove", string cancelText = "Cancel")
        {
            return await Task.FromResult(true);
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            await Task.CompletedTask;
        }

        public async Task ShowSuccessAsync(string title, string message)
        {
            await Task.CompletedTask;
        }
    }
}