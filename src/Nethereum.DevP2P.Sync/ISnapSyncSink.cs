using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Sink for streaming snap/1 payloads (accounts, storage slots, bytecodes) into a
    /// concrete state store while sync is in progress. The sink owns the running state-root
    /// computation and the per-account storage-root validation; <see cref="SnapSyncClient"/>
    /// is a pure fetcher that pushes the wire response shape into the sink one entry at a time.
    ///
    /// Implementations:
    /// <list type="bullet">
    ///   <item><see cref="InMemorySnapSyncSink"/>: accumulates everything in a Dictionary-backed
    ///         <c>InMemoryTrieStorage</c>. Suitable for small chains (AppChain) and tests.</item>
    ///   <item><c>RocksDbSnapSyncSink</c> (separate package): streams into <c>IStateStore</c> via
    ///         <c>IChainStoreBundle</c>. Required for mainnet (~250 GB state).</item>
    /// </list>
    /// </summary>
    public interface ISnapSyncSink
    {
        /// <summary>Called once at the start of a snap-sync run, before any other method.</summary>
        ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct);

        /// <summary>Append one account in slim-RLP form (as it appears on the snap/1 wire).</summary>
        ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct);

        /// <summary>
        /// Open a per-account storage scope. Called once per account whose canonical
        /// <c>StateRoot</c> is non-empty. After this, slots for this account arrive via
        /// <see cref="WriteStorageSlotAsync"/> until <see cref="EndAccountStorageAsync"/>.
        /// The sink is responsible for verifying the storage trie root matches
        /// <paramref name="expectedStorageRoot"/> when the scope closes.
        /// </summary>
        ValueTask BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct);

        /// <summary>Append one storage slot in the currently-open per-account scope.</summary>
        ValueTask WriteStorageSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct);

        /// <summary>Close the currently-open per-account storage scope and validate its root.</summary>
        ValueTask EndAccountStorageAsync(CancellationToken ct);

        /// <summary>
        /// Abort the currently-open per-account storage scope and discard any
        /// buffered slot writes for this account. Called when a sub-range fetch
        /// fails partway through the 16-way concurrent storage walk for a large
        /// contract, so the partial slots do not pollute the storage trie that
        /// the heal phase will reconcile. Closes the scope just like
        /// <see cref="EndAccountStorageAsync"/>; the difference is that nothing
        /// the caller wrote into the open scope is persisted.
        /// </summary>
        ValueTask AbortAccountStorageAsync(CancellationToken ct);

        /// <summary>Append one bytecode entry indexed by Keccak(code).</summary>
        ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct);

        /// <summary>
        /// Finalise the running state-root computation and return it. The caller compares
        /// against the target root; the sink does NOT throw on mismatch — that decision is
        /// the client's so it can attribute the failure to the offending peer.
        /// </summary>
        ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct);
    }
}
