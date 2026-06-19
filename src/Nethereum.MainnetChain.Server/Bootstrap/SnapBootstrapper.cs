using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.RocksDB.Snap;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Bootstrap
{
    /// <summary>
    /// Orchestrates the snap-first cold-start sequence:
    /// 1. Skip if the bundle already has committed state (<see cref="IChainMetadataStore.GetLastBlock"/> &gt; 0).
    /// 2. Run <see cref="SnapSyncClient.SyncStateAsync"/> against the provided
    ///    snap peer, streaming account + storage + bytecode into
    ///    <see cref="RocksDbSnapSyncSink"/> (which writes Patricia trie nodes
    ///    + bytecode CF entries inside the bundle).
    /// 3. The client throws on root mismatch — propagated to the caller.
    /// 4. On success, persist the pivot header into <see cref="IChainStoreBundle.Blocks"/>
    ///    and commit the metadata cursor at the pivot so the follower picks up
    ///    at pivot+1.
    ///
    /// <para>
    /// Cold reads against the snap-bootstrapped state must go through a
    /// <see cref="Nethereum.CoreChain.State.TrieFallbackStateStore"/> decorator
    /// over the inner <c>IStateStore</c>; this bootstrapper only writes trie
    /// nodes + bytecode, never flat account/storage rows.
    /// </para>
    /// </summary>
    public static class SnapBootstrapper
    {
        public sealed class Result
        {
            public bool Ran { get; init; }
            public string SkipReason { get; init; }
            public ulong PivotBlockNumber { get; init; }
            public byte[] PivotStateRoot { get; init; }
            public int AccountCount { get; init; }
            public int SlotCount { get; init; }
            public int BytecodeCount { get; init; }
        }

        public static async Task<Result> RunAsync(
            IChainStoreBundle bundle,
            ISnapPeer peer,
            BlockHeader pivot,
            byte[] pivotHash,
            ILogger logger,
            CancellationToken ct = default)
        {
            if (bundle is null) throw new ArgumentNullException(nameof(bundle));
            if (peer is null) throw new ArgumentNullException(nameof(peer));
            if (pivot is null) throw new ArgumentNullException(nameof(pivot));
            if (pivotHash is null || pivotHash.Length != 32)
                throw new ArgumentException("pivotHash must be 32 bytes", nameof(pivotHash));
            if (pivot.StateRoot is null || pivot.StateRoot.Length != 32)
                throw new ArgumentException("pivot.StateRoot must be 32 bytes", nameof(pivot));
            logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            var existing = bundle.Metadata.GetLastBlock();
            if (existing > 0)
            {
                logger.LogInformation(
                    "Snap-bootstrap: skip — bundle already has state at block {Block}.",
                    existing);
                return new Result { Ran = false, SkipReason = $"existing state at block {existing}" };
            }

            logger.LogInformation(
                "Snap-bootstrap: starting fetch at pivot block={Block} hash=0x{Hash} stateRoot=0x{Root}",
                pivot.BlockNumber, pivotHash.ToHex(), pivot.StateRoot.ToHex());

            var sink = new RocksDbSnapSyncSink(bundle.TrieNodes, bundle.State);
            var client = new SnapSyncClient(peer, sink);

            var syncResult = await client.SyncStateAsync(pivot.StateRoot, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Snap-bootstrap: state populated — {Accounts} accounts, {Slots} storage slots, {Codes} bytecodes (computed root matches pivot: {Match}).",
                sink.AccountCount, sink.SlotCount, sink.BytecodeCount, syncResult.RootMatchesTarget);

            await bundle.Blocks.SaveAsync(pivot, pivotHash).ConfigureAwait(false);
            bundle.Metadata.Commit((ulong)pivot.BlockNumber, pivotHash);
            if (bundle.Metadata.GetLastFetchedHeader() < (ulong)pivot.BlockNumber)
                bundle.Metadata.SetLastFetchedHeader((ulong)pivot.BlockNumber);
            if (bundle.Metadata.GetLastFetchedBody() < (ulong)pivot.BlockNumber)
                bundle.Metadata.SetLastFetchedBody((ulong)pivot.BlockNumber);

            return new Result
            {
                Ran = true,
                PivotBlockNumber = (ulong)pivot.BlockNumber,
                PivotStateRoot = pivot.StateRoot,
                AccountCount = sink.AccountCount,
                SlotCount = sink.SlotCount,
                BytecodeCount = sink.BytecodeCount,
            };
        }
    }
}
