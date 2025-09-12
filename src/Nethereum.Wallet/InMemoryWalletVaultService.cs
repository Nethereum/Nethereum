#nullable enable

using System.Threading.Tasks;

namespace Nethereum.Wallet;

public class InMemoryWalletVaultService : WalletVaultServiceBase
{
    private string? _encrypted;

    public override Task<bool> VaultExistsAsync()
        => Task.FromResult(!string.IsNullOrEmpty(_encrypted));

    protected override Task ResetStorageAsync()
    {
        _encrypted = null;
        
        return Task.CompletedTask;
    }

    protected override Task<string?> GetEncryptedAsync()
        => Task.FromResult(_encrypted);

    protected override Task SaveEncryptedAsync(string encrypted)
    {
        _encrypted = encrypted;
        return Task.CompletedTask;
    }
}
