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

        /// <summary>
        /// Stream every account without materialising the full state set into
        /// memory. Mainnet has hundreds of millions of accounts; a single
        /// Dictionary&lt;string, Account&gt; of that size OOMs. Use this for
        /// state-root rebuild / snapshot export / proof-tree construction —
        /// any walk where the consumer processes one account at a time and
        /// doesn't need an in-memory map. Order is implementation-defined and
        /// must not be relied on.
        /// </summary>
        IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync();

        Task<byte[]> GetStorageAsync(string address, BigInteger slot);
        Task SaveStorageAsync(string address, BigInteger slot, byte[] value);

        /// <summary>
        /// Write a storage slot using a precomputed <c>keccak(slot)</c> key
        /// (32 bytes). Reserved for the persistent state-diff store rewind
        /// path, which records pre-values keyed by their canonical
        /// storage-trie path and cannot reconstruct the original
        /// <see cref="BigInteger"/> slot. Block-execution callers must
        /// continue to use <see cref="SaveStorageAsync"/>.
        /// </summary>
        Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value);

        /// <summary>
        /// Every storage slot for <paramref name="address"/>, keyed by
        /// <c>keccak256(slotBytes32)</c> — the canonical storage-trie path
        /// (Yellow Paper §4.1). Returning the keccak directly lets Patricia
        /// trie / proof / state-root callers consume the value without
        /// re-hashing on every iteration. Binary-trie chains (which derive
        /// keys from raw slot bytes) consume a different backend entirely
        /// — see <c>RocksDbBinaryTrieStorage</c>.
        /// Keys use byte-content equality via <see cref="Nethereum.Util.ByteArrayComparer.Current"/>.
        /// </summary>
        Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address);
        Task ClearStorageAsync(string address);

        Task<byte[]> GetCodeAsync(byte[] codeHash);
        Task SaveCodeAsync(byte[] codeHash, byte[] code);

        Task<IStateSnapshot> CreateSnapshotAsync();
        Task CommitSnapshotAsync(IStateSnapshot snapshot);
        Task RevertSnapshotAsync(IStateSnapshot snapshot);

        Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync();
        Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address);
        /// <summary>
        /// Addresses whose entire storage was wiped via <see cref="ClearStorageAsync"/>
        /// since the last <see cref="ClearDirtyTrackingAsync"/>. The incremental
        /// state-root calculator uses this to drop cached storage tries for
        /// SELFDESTRUCTed contracts — without it, a contract destroyed and then
        /// re-materialised as an empty leaf in the same block keeps the stale
        /// pre-destruct storage trie, corrupting the leaf's storageRoot.
        /// First mainnet hit: block 116,525 (`0x4d95fbaf…` Killer pattern).
        /// </summary>
        Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync();
        Task ClearDirtyTrackingAsync();
    }
}
