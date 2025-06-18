using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public abstract class WalletVaultServiceBase : IWalletVaultService
{
    protected WalletVault? _vault;

    public Task<IReadOnlyList<IWalletAccount>> GetAccountsAsync()
        => Task.FromResult((IReadOnlyList<IWalletAccount>)(_vault?.Accounts ?? new List<IWalletAccount>()));

    public async Task CreateNewAsync(string password)
    {
        _vault = new WalletVault();
        await SaveAsync(password);
    }

    public async Task CreateNewVaultWithAccountAsync(string password, IWalletAccount account)
    {
        _vault = new WalletVault();
        _vault.AddAccount(account);
        await SaveAsync(password);
    }

    public WalletVault? GetCurrentVault() => _vault;

    public async Task SaveAsync(string password)
    {
        if (_vault == null) throw new InvalidOperationException("Vault not initialised");
        var encrypted = _vault.Encrypt(password);
        await SaveEncryptedAsync(encrypted);
    }

    public async Task<bool> UnlockAsync(string password)
    {
        var encrypted = await GetEncryptedAsync();
        if (string.IsNullOrEmpty(encrypted)) return false;

        var tmp = new WalletVault();
        try
        {
            tmp.Decrypt(encrypted, password);
        }
        catch
        {
            return false;
        }

        _vault = tmp;
        return true;
    }

    public abstract Task<bool> VaultExistsAsync();
    protected abstract Task<string?> GetEncryptedAsync();
    protected abstract Task SaveEncryptedAsync(string encrypted);
}
