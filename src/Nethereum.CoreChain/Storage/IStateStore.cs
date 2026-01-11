using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateStore
    {
        Task<Account> GetAccountAsync(string address);
        Task SaveAccountAsync(string address, Account account);
        Task<bool> AccountExistsAsync(string address);
        Task DeleteAccountAsync(string address);
        Task<Dictionary<string, Account>> GetAllAccountsAsync();

        Task<byte[]> GetStorageAsync(string address, BigInteger slot);
        Task SaveStorageAsync(string address, BigInteger slot, byte[] value);
        Task<Dictionary<BigInteger, byte[]>> GetAllStorageAsync(string address);
        Task ClearStorageAsync(string address);

        Task<byte[]> GetCodeAsync(byte[] codeHash);
        Task SaveCodeAsync(byte[] codeHash, byte[] code);

        Task<IStateSnapshot> CreateSnapshotAsync();
        Task CommitSnapshotAsync(IStateSnapshot snapshot);
        Task RevertSnapshotAsync(IStateSnapshot snapshot);
    }
}
