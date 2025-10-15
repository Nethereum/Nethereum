using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public interface IWalletVaultService
{
    Task<bool> UnlockAsync(string password);
    Task<IReadOnlyList<IWalletAccount>> GetAccountsAsync();
    Task<IReadOnlyList<AccountGroup>> GetAccountGroupsAsync();
    Task<bool> VaultExistsAsync();
    Task CreateNewAsync(string password);
    Task ResetAsync();
    WalletVault? GetCurrentVault();
    Task SaveAsync(string password);
    Task SaveAsync();
    Task CreateNewVaultWithAccountAsync(string password, IWalletAccount account);
    Task LockAsync();
}
