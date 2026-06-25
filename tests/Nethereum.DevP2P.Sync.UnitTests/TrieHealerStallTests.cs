using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    /// <summary>
    /// Tests for E-2: heal stall-detection exit. When PivotRefresher returns
    /// the same root (no rotation possible — pivot truly stalled), the loop
    /// must NOT busy-spin to MaxRounds=100k. After StallThresholdRounds (32)
    /// consecutive empty rounds with no rotation, the healer exits with a
    /// non-convergence HealResult. If the queue happens to be empty AND the
    /// computed root matches the target, exits with Matched=true instead
    /// (early-convergence detector).
    /// </summary>
    public class TrieHealerStallTests
    {
        [Fact]
        public async Task Heal_EarlyConvergenceOnRootMatch_ExitsBeforeMaxRounds()
        {
            // Build a tiny single-leaf trie. Seed the storage with the root
            // node. Scheduler serves the root node on demand, so round 1
            // resolves the queue (single leaf → no children to enqueue) and
            // the natural while-loop exit hits the computed-root validation
            // (TrieHealer.cs:220-235) which returns Matched=true. This must
            // happen well before MaxRounds=100k.
            var storage = new InMemoryTrieStorage();
            var (rootHash, leafBlob) = SeedSingleLeafTrie(storage);

            var scheduler = new FakeFetchScheduler { ServeOnceNode = leafBlob };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);
            healer.PivotRefresher = ct => Task.FromResult(rootHash);

            var result = await healer.HealAsync(rootHash, CancellationToken.None);

            Assert.True(result.Matched, "should converge once queue drains and root matches");
            Assert.True(scheduler.FetchCalls < 1_000,
                $"should exit well before MaxRounds (100k); FetchCalls={scheduler.FetchCalls}");
            Assert.Equal(rootHash, result.FinalTargetRoot);
        }

        [Fact]
        public async Task Heal_PivotRefreshUnchanged_StallCounterNotReset()
        {
            // Target a root the storage does NOT contain. Scheduler returns
            // empty nodes forever (peers serving zero useful nodes). With no
            // pivot rotation the stall counter must reach the threshold and
            // the healer must exit — NOT spin to MaxRounds=100k.
            var storage = new InMemoryTrieStorage();
            var targetRoot = new byte[32];
            for (int i = 0; i < 32; i++) targetRoot[i] = 0xAB;

            var scheduler = new FakeFetchScheduler { ReturnNoNodes = true };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);
            int refreshCalls = 0;
            healer.PivotRefresher = ct =>
            {
                Interlocked.Increment(ref refreshCalls);
                return Task.FromResult(targetRoot);
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await healer.HealAsync(targetRoot, cts.Token);

            Assert.False(result.Matched, "stalled heal cannot converge when peers serve zero nodes");
            Assert.True(scheduler.FetchCalls <= 200,
                $"exit must happen at the stall threshold (32 rounds), not at MaxRounds; FetchCalls={scheduler.FetchCalls}");
            Assert.True(refreshCalls >= 1, "pivot refresh must have been polled at least once before stall exit");
        }

        [Fact]
        public async Task Heal_RealProgress_StallCounterResets()
        {
            // Scheduler that returns the requested root node on the FIRST call
            // and zero nodes on subsequent ones. The storage already contains
            // the node (placed there by seed) so processed > 0 → queue drains
            // → loop exits via natural queue.Count == 0. The early-convergence
            // detector should not fire (we never crossed the stall threshold).
            var storage = new InMemoryTrieStorage();
            var (rootHash, leafBlob) = SeedSingleLeafTrie(storage);

            var scheduler = new FakeFetchScheduler { ServeOnceNode = leafBlob };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);
            healer.PivotRefresher = ct => Task.FromResult(rootHash);

            var result = await healer.HealAsync(rootHash, CancellationToken.None);

            Assert.True(result.Matched, "natural convergence on tiny trie should succeed");
            Assert.True(scheduler.FetchCalls <= 4,
                $"happy path should take a small number of rounds; FetchCalls={scheduler.FetchCalls}");
        }

        // ---------- helpers ----------

        private static (byte[] root, byte[] leafBlob) SeedSingleLeafTrie(ITrieStorage storage)
        {
            var trie = new PatriciaTrie();
            var key = new byte[] { 0xAA };
            var value = new byte[] { 0x42 };
            trie.Put(key, value, storage);
            trie.SaveNodesToStorage(storage);
            var root = trie.Root.GetHash();
            var blob = storage.Get(root);
            return (root, blob);
        }

        private sealed class FakeFetchScheduler : IFetchRequestScheduler
        {
            public int FetchCalls;
            public bool ReturnNoNodes;
            public byte[]? ServeOnceNode;
            private int _served;

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref FetchCalls);
                var msg = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                if (ReturnNoNodes)
                {
                    for (int i = 0; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                    return Task.FromResult(msg);
                }
                if (ServeOnceNode != null && Interlocked.CompareExchange(ref _served, 1, 0) == 0)
                {
                    msg.Nodes.Add(ServeOnceNode);
                    for (int i = 1; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                    return Task.FromResult(msg);
                }
                for (int i = 0; i < paths.Count; i++) msg.Nodes.Add(Array.Empty<byte>());
                return Task.FromResult(msg);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}
