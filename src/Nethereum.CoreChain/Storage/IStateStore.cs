using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateStore
    {
        Task<Account> GetAccountAsync(string address);
        Task<Account> GetAccountAsync(EvmAddress address) => GetAccountAsync(address.ToHexLower());

        Task SaveAccountAsync(string address, Account account);
        Task SaveAccountAsync(EvmAddress address, Account account) => SaveAccountAsync(address.ToHexLower(), account);

        Task<bool> AccountExistsAsync(string address);
        Task<bool> AccountExistsAsync(EvmAddress address) => AccountExistsAsync(address.ToHexLower());

        Task DeleteAccountAsync(string address);
        Task DeleteAccountAsync(EvmAddress address) => DeleteAccountAsync(address.ToHexLower());

        Task<Dictionary<string, Account>> GetAllAccountsAsync();

        IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync();

        Task<byte[]> GetStorageAsync(string address, BigInteger slot);
        Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot) => GetStorageAsync(address.ToHexLower(), slot);

        Task SaveStorageAsync(string address, BigInteger slot, byte[] value);
        Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value) => SaveStorageAsync(address.ToHexLower(), slot, value);

        Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value);
        Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value) => SaveStorageByKeccakAsync(address.ToHexLower(), slotKeccak, value);

        Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address);
        Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address) => GetAllStorageAsync(address.ToHexLower());

        Task ClearStorageAsync(string address);
        Task ClearStorageAsync(EvmAddress address) => ClearStorageAsync(address.ToHexLower());

        Task<byte[]> GetCodeAsync(byte[] codeHash);
        Task SaveCodeAsync(byte[] codeHash, byte[] code);

        Task<IStateSnapshot> CreateSnapshotAsync();
        Task CommitSnapshotAsync(IStateSnapshot snapshot);
        Task RevertSnapshotAsync(IStateSnapshot snapshot);

        Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync();
        Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address);
        Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync();
        Task ClearDirtyTrackingAsync();
    }
}
