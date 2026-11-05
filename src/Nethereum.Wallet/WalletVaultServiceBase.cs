#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public abstract class WalletVaultServiceBase : IWalletVaultService
{
    protected WalletVault? _vault;
    protected string? _currentPassword;
    protected virtual IEncryptionStrategy GetEncryptionStrategy() => new Aes256GcmEncryptionStrategy();

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

        var hardwareInfo = _vault?.FindHardwareDevice(groupId);
        if (hardwareInfo != null)
        {
            return hardwareInfo;
        }

        return null;
    }

    public virtual async Task CreateNewAsync(string password)
    {
        VaultIntegrityCheckFailed = false;
        _vault = new WalletVault(GetEncryptionStrategy());
        _currentPassword = password;
        await SaveAsync(password);
    }

    public virtual async Task CreateNewVaultWithAccountAsync(string password, IWalletAccount account)
    {
        VaultIntegrityCheckFailed = false;
        _vault = new WalletVault(GetEncryptionStrategy());
        _vault.AddAccount(account);
        _currentPassword = password;
        await SaveAsync(password);
    }

    public bool VaultIntegrityCheckFailed { get; private set; }

    public IReadOnlyList<IWalletAccount> AccountsWithLegacySeed
        => _vault?.AccountsWithLegacySeed ?? new List<IWalletAccount>();

    public bool HasAccountsWithLegacySeed => AccountsWithLegacySeed.Count > 0;

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
        catch (Exception)
        {
            return false;
        }

        _vault = await MigrateToCurrentFormatAsync(tmp, encrypted, password);
        _currentPassword = password;
        return true;
    }

    protected virtual async Task<WalletVault> MigrateToCurrentFormatAsync(WalletVault unlocked, string encrypted, string password)
    {
        VaultIntegrityCheckFailed = false;
        try
        {
            if (!IsFirstPartyStrategy(GetEncryptionStrategy())) return unlocked;
            var blob = Convert.FromBase64String(encrypted);
            if (Aes256GcmEncryptionStrategy.IsCurrentFormat(blob)) return unlocked;
            var isKeyStoreFormat = LegacyVaultDecryptor.IsKeyStoreFormat(blob);
            if (!isKeyStoreFormat && !await unlocked.IsConsistentAsync())
            {
                VaultIntegrityCheckFailed = true;
                unlocked.SetEncryptionStrategy(new LegacyAesEncryptionStrategy(!UsesSha256Variant(blob, password)));
                return unlocked;
            }

            unlocked.SetEncryptionStrategy(new Aes256GcmEncryptionStrategy());
            await SaveEncryptedAsync(unlocked.Encrypt(password));
        }
        catch (Exception)
        {
        }
        return unlocked;
    }

    private static bool UsesSha256Variant(byte[] blob, string password)
    {
        try
        {
            return LegacyVaultDecryptor.IsJsonObject(LegacyVaultDecryptor.DecryptAesSha256(blob, password));
        }
        catch (Exception)
        {
            return false;
        }
    }

#pragma warning disable CS0618
    private static bool IsFirstPartyStrategy(IEncryptionStrategy strategy)
        => strategy is Aes256GcmEncryptionStrategy
            || strategy is DefaultAes256EncryptionStrategy
            || strategy is BouncyCastleAes256EncryptionStrategy;
#pragma warning restore CS0618

    public abstract Task<bool> VaultExistsAsync();
    
    public virtual async Task ResetAsync()
    {
        VaultIntegrityCheckFailed = false;
        await ResetStorageAsync();
        _vault = null;
        _currentPassword = null;
    }

    public virtual Task LockAsync()
    {
        VaultIntegrityCheckFailed = false;
        _vault = null;
        _currentPassword = null;
        return Task.CompletedTask;
    }

    protected abstract Task ResetStorageAsync();
    protected abstract Task<string?> GetEncryptedAsync();
    protected abstract Task SaveEncryptedAsync(string encrypted);
}
