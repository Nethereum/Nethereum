using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Nethereum.Signer.Trezor.Abstractions;

namespace Nethereum.Signer.Trezor.Maui.Services
{
    public class MauiPromptHandler : ITrezorPromptHandler
    {
        public Task ButtonAckAsync(string context)
        {
            return MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current?.MainPage?.DisplayAlert("Trezor", context ?? "Confirm the action on your device.", "OK") ?? Task.CompletedTask);
        }

        public Task<string> GetPassphraseAsync()
        {
            return PromptAsync("Enter passphrase", "Enter your Trezor passphrase (leave blank for standard wallet).", Keyboard.Text);
        }

        public Task<string> GetPinAsync()
        {
            const string message = "Use the Trezor keypad mapping (device rows map to 7 8 9 / 4 5 6 / 1 2 3).";
            return PromptAsync("Enter PIN", message, Keyboard.Numeric);
        }

        private async Task<string> PromptAsync(string title, string message, Keyboard keyboard)
        {
            var response = await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current?.MainPage?.DisplayPromptAsync(title, message, "OK", "Cancel", "", -1, keyboard)) ?? string.Empty;

            return response?.Trim() ?? string.Empty;
        }
    }
}
