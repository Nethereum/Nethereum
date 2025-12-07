#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Wallet.Diagnostics;
using Nethereum.Wallet.UI.Components.Blazor.Services;

namespace Nethereum.Wallet.UI.Components.Blazor.Trezor.Prompts;

public class BlazorTrezorPromptHandler : ITrezorPromptHandler
{
    private readonly IWalletDialogAccessor _dialogAccessor;
    private string? _cachedPassphrase;
    private static int _nextInstanceId;
    private readonly int _instanceId;

    public BlazorTrezorPromptHandler(IWalletDialogAccessor dialogAccessor)
    {
        _dialogAccessor = dialogAccessor ?? throw new ArgumentNullException(nameof(dialogAccessor));
        _instanceId = Interlocked.Increment(ref _nextInstanceId);
        Log($"Handler #{_instanceId} created.");
    }

    public async Task<string> GetPinAsync()
    {
        Log("PIN prompt requested");
        var pin = await ShowDialogAsync<TrezorPinPrompt>();
        if (pin == null)
        {
            Log("PIN prompt canceled by user");
            throw new OperationCanceledException("PIN prompt canceled by user");
        }

        Log($"PIN prompt completed (length={pin.Length})");
        return pin;
    }

    public async Task<string> GetPassphraseAsync()
    {
        if (_cachedPassphrase != null)
        {
            Log("Reusing cached passphrase");
            return _cachedPassphrase;
        }

        Log("Passphrase prompt requested");
        var passphrase = await ShowDialogAsync<TrezorPassphrasePrompt>();
        if (passphrase == null)
        {
            Log("Passphrase prompt cancelled");
            throw new OperationCanceledException("Passphrase prompt canceled by user");
        }

        _cachedPassphrase = passphrase;
        Log($"Passphrase prompt completed (length={passphrase.Length})");
        return passphrase;
    }

    public Task ButtonAckAsync(string context)
    {
        Log($"ButtonAck: {context}");
        return Task.CompletedTask;
    }

    private async Task<string?> ShowDialogAsync<TComponent>()
        where TComponent : IComponent
    {
        Log($"Handler #{_instanceId} showing dialog {typeof(TComponent).Name}");

        var dialogService = await WaitForDialogServiceAsync().ConfigureAwait(false);

        IDialogReference dialog;
        try
        {
            dialog = await WalletBlazorDispatcher.RunAsync(() =>
            {
                Log($"Creating dialog {typeof(TComponent).Name}");
                return dialogService.ShowAsync<TComponent>(string.Empty);
            });
        }
        catch (Exception ex)
        {
            Log($"Error creating dialog {typeof(TComponent).Name}: {ex.Message}");
            throw;
        }

        DialogResult result;
        try
        {
            result = await WalletBlazorDispatcher.RunAsync(() =>
            {
                Log($"Awaiting dialog result {typeof(TComponent).Name}");
                return dialog.Result;
            });
        }
        catch (Exception ex)
        {
            Log($"Error awaiting dialog result {typeof(TComponent).Name}: {ex.Message}");
            throw;
        }

        if (result == null || result.Canceled)
        {
            Log($"Dialog {typeof(TComponent).Name} canceled or null result");
            return null;
        }

        var data = result.Data switch
        {
            string text => text,
            _ => string.Empty
        };

        Log($"Dialog {typeof(TComponent).Name} returned length={data.Length}");
        return data;
    }

    private async Task<IDialogService> WaitForDialogServiceAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var dialogService = _dialogAccessor.DialogService;
            if (dialogService != null)
            {
                return dialogService;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Log("Dialog service still unavailable after timeout.");
                throw new InvalidOperationException("Dialog service is not available.");
            }

            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private void Log(string message)
    {
        WalletDiagnosticsLogger.Log("BlazorPrompt", message);
    }
}
