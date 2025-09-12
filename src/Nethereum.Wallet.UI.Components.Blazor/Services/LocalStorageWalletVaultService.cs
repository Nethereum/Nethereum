using Microsoft.JSInterop;
using Nethereum.Wallet;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class LocalStorageWalletVaultService : WalletVaultServiceBase
    {
        private const string LocalStorageKey = "Nethereum.Wallet.Vault";
        private readonly IJSRuntime _jsRuntime;
        private readonly IEncryptionStrategy _encryptionStrategy;

        public LocalStorageWalletVaultService(IJSRuntime jsRuntime, IEncryptionStrategy encryptionStrategy)
        {
            _jsRuntime = jsRuntime;
            _encryptionStrategy = encryptionStrategy;
        }

        protected override IEncryptionStrategy GetEncryptionStrategy() => _encryptionStrategy;

        public override async Task<bool> VaultExistsAsync()
        {
            var encryptedJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
            return !string.IsNullOrEmpty(encryptedJson);
        }

        protected override async Task ResetStorageAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LocalStorageKey);
        }

        protected override async Task<string?> GetEncryptedAsync()
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
        }

        protected override async Task SaveEncryptedAsync(string encrypted)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, encrypted);
        }
    }
}