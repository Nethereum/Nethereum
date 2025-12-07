using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.Maui.Services
{
    public class MauiSecureStorageWalletVaultService : WalletVaultServiceBase
    {
        private const string StorageKey = "wallet.vault";

        public override async Task<bool> VaultExistsAsync()
        {
            var value = await SecureStorage.GetAsync(StorageKey);
            return !string.IsNullOrEmpty(value);
        }

        protected override Task<string?> GetEncryptedAsync()
        {
            return SecureStorage.GetAsync(StorageKey);
        }

        protected override Task SaveEncryptedAsync(string encrypted)
        {
            return SecureStorage.SetAsync(StorageKey, encrypted);
        }

        protected override Task ResetStorageAsync()
        {
            try
            {
                SecureStorage.Remove(StorageKey);
            }
            catch
            {
                // Ignore platform exceptions during reset to avoid crashing logout flows.
            }

            return Task.CompletedTask;
        }
    }
}
