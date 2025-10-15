#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public abstract class WalletVaultServiceBase : IWalletVaultService
{
    protected WalletVault? _vault;
    protected string? _currentPassword;
    protected virtual IEncryptionStrategy GetEncryptionStrategy() => new DefaultAes256EncryptionStrategy();

    public Task<IReadOnlyList<IWalletAccount>> GetAccountsAsync()
        => Task.FromResult((IReadOnlyList<IWalletAccount>)(_vault?.Accounts ?? new List<IWalletAccount>()));

    public Task<IReadOnlyList<AccountGroup>> GetAccountGroupsAsync()
    {
        var accounts = _vault?.Accounts ?? new List<IWalletAccount>();
        var groups = new List<AccountGroup>();
        
        var accountsWithGroup = accounts.Where(a => !string.IsNullOrEmpty(a.GroupId)).ToList();
        foreach (var group in accountsWithGroup.GroupBy(account => account.GroupId))
        {
            object? groupMetadata = null;
            if (!string.IsNullOrEmpty(group.Key))
            {
                groupMetadata = GetGroupMetadata(group.Key);
            }
            
            groups.Add(new AccountGroup(group.Key, group, groupMetadata));
        }
        
        // Handle accounts without GroupId - group them by account type
        var accountsWithoutGroup = accounts.Where(a => string.IsNullOrEmpty(a.GroupId)).ToList();
        foreach (var typeGroup in accountsWithoutGroup.GroupBy(account => account.Type))
        {
            // Use account type as the group ID for accounts without explicit grouping
            groups.Add(new AccountGroup($"type:{typeGroup.Key}", typeGroup));
        }
        
        return Task.FromResult((IReadOnlyList<AccountGroup>)groups);
    }
    protected virtual object? GetGroupMetadata(string groupId)
    {
        var mnemonicInfo = _vault?.Mnemonics?.FirstOrDefault(m => m.Id == groupId);
        if (mnemonicInfo != null)
        {
            return mnemonicInfo;
        }
        
        return null;
    }

    public virtual async Task CreateNewAsync(string password)
    {
        _vault = new WalletVault(GetEncryptionStrategy());
        _currentPassword = password;
        await SaveAsync(password);
    }

    public virtual async Task CreateNewVaultWithAccountAsync(string password, IWalletAccount account)
    {
        _vault = new WalletVault(GetEncryptionStrategy());
        _vault.AddAccount(account);
        _currentPassword = password;
        await SaveAsync(password);
    }

    public WalletVault? GetCurrentVault() => _vault;

    public async Task SaveAsync(string password)
    {
        if (_vault == null) throw new InvalidOperationException("Vault not initialised");
        var encrypted = _vault.Encrypt(password);
        await SaveEncryptedAsync(encrypted);
    }

    public async Task SaveAsync()
    {
        if (_currentPassword == null) throw new InvalidOperationException("No current password available. Vault must be unlocked first.");
        await SaveAsync(_currentPassword);
    }

    public virtual async Task<bool> UnlockAsync(string password)
    {
        var encrypted = await GetEncryptedAsync();
        if (string.IsNullOrEmpty(encrypted)) return false;

        var tmp = new WalletVault(GetEncryptionStrategy());
        try
        {
            tmp.Decrypt(encrypted, password);
        }
        catch
        {
            return false;
        }

        _vault = tmp;
        _currentPassword = password;
        return true;
    }

    public abstract Task<bool> VaultExistsAsync();
    
    public virtual async Task ResetAsync()
    {
        await ResetStorageAsync();
        _vault = null;
        _currentPassword = null;
    }

    public virtual Task LockAsync()
    {
        _vault = null;
        _currentPassword = null;
        return Task.CompletedTask;
    }

    protected abstract Task ResetStorageAsync();
    protected abstract Task<string?> GetEncryptedAsync();
    protected abstract Task SaveEncryptedAsync(string encrypted);
}
