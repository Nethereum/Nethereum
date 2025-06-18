using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public interface IWalletVaultService
{
    /// <summary>
    /// Loads the encrypted vault from storage and decrypts it with <paramref name="password"/>.
    /// </summary>
    Task<bool> UnlockAsync(string password);

    /// <summary>
    /// Returns all accounts currently inside the open vault.
    /// </summary>
    Task<IReadOnlyList<IWalletAccount>> GetAccountsAsync();

    /// <summary>
    /// Returns <c>true</c> if a vault exists on disk/storage.
    /// </summary>
    Task<bool> VaultExistsAsync();

    /// <summary>
    /// Creates a brand‑new empty vault and persists it encrypted with <paramref name="password"/>.
    /// </summary>
    Task CreateNewAsync(string password);
    WalletVault GetCurrentVault();
    Task SaveAsync(string password);
    Task CreateNewVaultWithAccountAsync(string password, IWalletAccount account);
}
