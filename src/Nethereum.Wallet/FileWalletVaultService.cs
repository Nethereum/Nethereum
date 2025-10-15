#nullable enable

using System.IO;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public class FileWalletVaultService : WalletVaultServiceBase
{
    private readonly string _filePath;
    private readonly IEncryptionStrategy? _strategy;

    public FileWalletVaultService(string filePath)
    {
        _filePath = filePath;
    }

    public FileWalletVaultService(string filePath, IEncryptionStrategy strategy)
    {
        _filePath = filePath;
        _strategy = strategy;
    }

    protected override IEncryptionStrategy GetEncryptionStrategy()
        => _strategy ?? base.GetEncryptionStrategy();

    public override Task<bool> VaultExistsAsync()
        => Task.FromResult(File.Exists(_filePath));

    protected override Task ResetStorageAsync()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
        
        return Task.CompletedTask;
    }

    protected override async Task<string?> GetEncryptedAsync()
    {
        if (!File.Exists(_filePath)) return null;
        return await File.ReadAllTextAsync(_filePath);
    }

    protected override async Task SaveEncryptedAsync(string encrypted)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_filePath, encrypted);
    }
}
