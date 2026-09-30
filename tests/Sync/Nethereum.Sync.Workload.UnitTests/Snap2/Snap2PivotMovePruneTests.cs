using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class Snap2PivotMovePruneTests
    {
        private static byte[] Filled(byte value) => Enumerable.Repeat(value, 32).ToArray();

        private static byte[] Hash(byte first, byte last)
        {
            var h = new byte[32];
            h[0] = first;
            h[31] = last;
            return h;
        }

        private static byte[] BuildState(InMemoryContentNodeStore store, params (byte[] Hash, ulong Balance)[] accounts)
        {
            var trie = new PatriciaTrie(store);
            foreach (var (hash, balance) in accounts)
                trie.Put(hash, new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)1,
                    Balance = (EvmUInt256)balance,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
            trie.SaveDirtyNodesToStorage();
            return trie.Root.GetHash();
        }

        private static SnapSyncAccountTask Chunk(byte[] next, byte[] last) => new SnapSyncAccountTask
        {
            Next = next,
            Last = last,
            StorageCompleted = Array.Empty<byte[]>(),
            SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current),
        };

        private sealed class HoldRangeAtRootPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly byte[] _root;
            private readonly byte[] _heldFrom;

            public HoldRangeAtRootPeer(ISnapPeer inner, byte[] root, byte[] heldFrom)
            {
                _inner = inner;
                _root = root;
                _heldFrom = heldFrom;
            }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                if (ByteUtil.AreEqual(r.RootHash, _root) && ByteUtil.AreEqual(r.StartingHash, _heldFrom))
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return await _inner.GetAccountRangeAsync(r, ct).ConfigureAwait(false);
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Given_AnAccountHashEqualToAChunkBoundary_When_ThePivotMovesAfterThatChunkCompletes_Then_ThePruneKeepsTheAccount()
        {
            var below = Hash(0x20, 0x01);
            var boundary = Filled(0x55);
            var above = Hash(0xa0, 0x03);
            var store = new InMemoryContentNodeStore();
            var rootA = BuildState(store, (below, 1), (boundary, 1), (above, 1));
            var rootB = BuildState(store, (below, 1), (boundary, 1), (above, 2));
            var secondChunkStart = SnapHashRanges.IncrementHash(boundary);

            var dir = Path.Combine(Path.GetTempPath(), $"snap2prune_{Guid.NewGuid():N}");
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(dir);
                var flat = (ISnapFlatStateWriter)bundle.State;
                var firstChunkDurable = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                IReadOnlyList<SnapSyncAccountTask> pruned = null;

                var peer = new HoldRangeAtRootPeer(
                    new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new HeadStateLoader.BytecodeStore())),
                    rootA, secondChunkStart);
                var client = new SnapSyncClient(peer, new TrieSnapSyncSink(new InMemoryContentNodeStore(), bundle.State))
                {
                    AccountConcurrency = 2,
                    RootRefreshIntervalMs = 15,
                    CheckpointBytesThreshold = 1,
                    PivotRefresher = _ => Task.FromResult(firstChunkDurable.Task.IsCompleted ? rootB : rootA),
                };
                client.PivotCatchUp = (tasks, ct) =>
                {
                    pruned = tasks;
                    ((IFlatStateTrieGenerator)bundle).PruneFlatStateBeyond(tasks);
                    return Task.FromResult(rootB);
                };

                var resumeFrom = new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Phase2Running,
                    PivotBlockNumber = 0,
                    PivotBlockHash = new byte[32],
                    HealTargetRoot = new byte[32],
                    Counters = SnapSyncCounters.Zero,
                    Tasks = new List<SnapSyncAccountTask> { Chunk(new byte[32], boundary), Chunk(secondChunkStart, Filled(0xff)) },
                };
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await client.SyncStateWithCheckpointAsync(rootA, resumeFrom,
                    cp =>
                    {
                        if (ByteUtil.AreEqual(cp.State.Tasks[0].Next, boundary)) firstChunkDurable.TrySetResult(true);
                    },
                    cts.Token);

                Assert.NotNull(pruned);
                Assert.NotNull(await flat.GetAccountByHashAsync(boundary));
                Assert.NotNull(await flat.GetAccountByHashAsync(below));
                Assert.Equal((EvmUInt256)2, (await flat.GetAccountByHashAsync(above)).Balance);
                var remaining = Assert.Single(pruned);
                Assert.Equal(secondChunkStart.ToHex(), remaining.Next.ToHex());
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
