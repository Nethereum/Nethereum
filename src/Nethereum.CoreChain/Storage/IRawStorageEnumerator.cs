using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Backend-specific extension on <see cref="IStateStore"/> for consumers
    /// that need raw, unhashed storage-slot iteration — specifically the
    /// EIP-7864 Binary trie, whose key derivation requires the raw 32-byte
    /// slot key (not keccak(slot)).
    ///
    /// <para>
    /// Patricia-shape stores (RocksDbStateStore, SqliteStateStore) hash slots
    /// via keccak per Yellow Paper §4.1 / EIP-2364 before persisting, so the
    /// raw BigInteger slot is not recoverable from them — they deliberately
    /// do not implement this interface. <see cref="InMemory.InMemoryStateStore"/>
    /// retains raw slots internally and exposes them here so Binary-mode
    /// chains running over an in-memory backend can compute full state roots.
    /// Production Binary chains must run on a backend that persists raw
    /// slots; this interface is the contract such a backend must satisfy.
    /// </para>
    /// </summary>
    public interface IRawStorageEnumerator
    {
        /// <summary>
        /// Stream every present (slot, value) pair under
        /// <paramref name="address"/> using the raw BigInteger slot. Cleared
        /// (zero) slots are not yielded.
        /// </summary>
        System.Collections.Generic.IAsyncEnumerable<KeyValuePair<BigInteger, byte[]>>
            StreamRawStorageAsync(string address);
    }
}
