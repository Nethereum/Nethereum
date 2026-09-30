using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TrieHealerStallTests
    {
        [Fact]
        public async Task Heal_EarlyConvergenceOnRootMatch_ExitsBeforeMaxRounds()
        {
            var storage = new InMemoryContentNodeStore();
            var (rootHash, leafBlob) = SeedSingleLeafTrie(storage);

            var scheduler = new FakeFetchScheduler { ServeOnceNode = leafBlob };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((rootHash, 0UL));

            var result = await healer.HealAsync(rootHash, ct: CancellationToken.None);

            Assert.True(result.Matched, "should converge once queue drains and root matches");
            Assert.True(scheduler.FetchCalls < 1_000,
                $"should exit well before MaxRounds (100k); FetchCalls={scheduler.FetchCalls}");
            Assert.Equal(rootHash, result.FinalTargetRoot);
        }

        [Fact]
        public async Task Heal_PivotRefreshUnchanged_StallCounterNotReset()
        {
            var storage = new InMemoryContentNodeStore();
            var targetRoot = new byte[32];
            for (int i = 0; i < 32; i++) targetRoot[i] = 0xAB;

            var scheduler = new FakeFetchScheduler { ReturnNoNodes = true };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);
            int refreshCalls = 0;
            healer.PivotRefresher = (_, ct) =>
            {
                Interlocked.Increment(ref refreshCalls);
                return Task.FromResult<(byte[] Root, ulong Block)?>((targetRoot, 0UL));
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await healer.HealAsync(targetRoot, ct: cts.Token);

            Assert.False(result.Matched, "stalled heal cannot converge when peers serve zero nodes");
            Assert.True(scheduler.FetchCalls <= 200,
                $"exit must happen at the stall threshold (32 rounds), not at MaxRounds; FetchCalls={scheduler.FetchCalls}");
            Assert.True(refreshCalls >= 1, "pivot refresh must have been polled at least once before stall exit");
        }

        [Fact]
        public async Task Heal_RealProgress_StallCounterResets()
        {
            var storage = new InMemoryContentNodeStore();
            var (rootHash, leafBlob) = SeedSingleLeafTrie(storage);

            var scheduler = new FakeFetchScheduler { ServeOnceNode = leafBlob };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((rootHash, 0UL));

            var result = await healer.HealAsync(rootHash, ct: CancellationToken.None);

            Assert.True(result.Matched, "natural convergence on tiny trie should succeed");
            Assert.True(scheduler.FetchCalls <= 4,
                $"happy path should take a small number of rounds; FetchCalls={scheduler.FetchCalls}");
        }

        [Fact]
        public async Task Heal_SeededFromAccountsNeedingHeal_FetchesMissingStorageSubtree()
        {
            var storage = new InMemoryContentNodeStore();

            var slotStorage = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(slotStorage);
            var slotKey = new byte[32]; slotKey[0] = 0x77;
            storageTrie.Put(slotKey, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x09 }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();
            var storageBlob = slotStorage.Get(storageRoot);

            var accountHash = new byte[32]; accountHash[0] = 0x33;
            var account = new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(storage);
            stateTrie.Put(accountHash, new AccountEncoder().Encode(account));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            Assert.Null(storage.Get(storageRoot));

            var scheduler = new FakeFetchScheduler { ServeOnceNode = storageBlob };
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance);

            var result = await healer.HealAsync(
                stateRoot, new[] { (accountHash, storageRoot) }, ct: CancellationToken.None);

            Assert.NotNull(storage.Get(storageRoot));
            Assert.True(result.Matched);
        }


        private static (byte[] root, byte[] leafBlob) SeedSingleLeafTrie(InMemoryContentNodeStore storage)
        {
            var trie = new PatriciaTrie(storage);
            var key = new byte[] { 0xAA };
            var value = new byte[] { 0x42 };
            trie.Put(key, value);
            trie.SaveNodesToStorage();
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
