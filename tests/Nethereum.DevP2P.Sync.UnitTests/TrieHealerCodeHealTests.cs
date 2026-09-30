using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
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
    public class TrieHealerCodeHealTests
    {
        [Fact]
        public async Task Heal_SeededCode_FetchesAndStoresDeferredBytecode()
        {
            var storage = new InMemoryContentNodeStore();
            var rootHash = SeedSingleLeafTrie(storage);

            var code = new byte[] { 0x60, 0x2A, 0xF3 };
            var codeHash = new Sha3KeccackHashProvider().ComputeHash(code);
            var codeStore = new InMemoryStateStore();
            Assert.Null(await codeStore.GetCodeAsync(codeHash));

            var scheduler = new CodeServingScheduler();
            scheduler.Codes[codeHash] = code;

            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance, codeStore: codeStore);

            var result = await healer.HealAsync(
                rootHash, seedCodeHeal: new[] { codeHash }, ct: CancellationToken.None);

            var stored = await codeStore.GetCodeAsync(codeHash);
            Assert.NotNull(stored);
            Assert.Equal(code, stored);
            Assert.Equal(codeHash, new Sha3KeccackHashProvider().ComputeHash(stored));
            Assert.True(result.Matched, "seeded-code heal must converge once the code is stored");
            Assert.True(scheduler.ByteCodeCalls >= 1, "heal must have issued a GetByteCodes request");
        }

        [Fact]
        public async Task Heal_SeededCode_TransientlyUnavailableThenServed_IsFetched()
        {
            var storage = new InMemoryContentNodeStore();
            var rootHash = SeedSingleLeafTrie(storage);

            var code = new byte[] { 0x60, 0x07, 0xF3 };
            var codeHash = new Sha3KeccackHashProvider().ComputeHash(code);
            var codeStore = new InMemoryStateStore();

            var scheduler = new CodeServingScheduler { ServeAfterCalls = 2 };
            scheduler.Codes[codeHash] = code;
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance, codeStore: codeStore)
            {
                NoPeerFailureBackoff = TimeSpan.FromMilliseconds(1),
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await healer.HealAsync(
                rootHash, seedCodeHeal: new[] { codeHash }, ct: cts.Token);

            Assert.Equal(code, await codeStore.GetCodeAsync(codeHash));
            Assert.True(result.Matched);
            Assert.True(scheduler.ByteCodeCalls >= 3, "the code must have been re-requested after the transient refusals");
        }

        [Fact]
        public async Task Heal_SeededCode_AlreadyPresent_NoWireFetch()
        {
            var storage = new InMemoryContentNodeStore();
            var rootHash = SeedSingleLeafTrie(storage);

            var code = new byte[] { 0x60, 0x01, 0xF3 };
            var codeHash = new Sha3KeccackHashProvider().ComputeHash(code);
            var codeStore = new InMemoryStateStore();
            await codeStore.SaveCodeAsync(codeHash, code);

            var scheduler = new CodeServingScheduler();
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance, codeStore: codeStore);

            var result = await healer.HealAsync(
                rootHash, seedCodeHeal: new[] { codeHash }, ct: CancellationToken.None);

            Assert.True(result.Matched);
            Assert.Equal(0, scheduler.ByteCodeCalls);
        }

        [Fact]
        public async Task Heal_SeededCode_NoPeerEverServes_ReportsNonConvergence()
        {
            var storage = new InMemoryContentNodeStore();
            var rootHash = SeedSingleLeafTrie(storage);

            var code = new byte[] { 0x60, 0x09, 0xF3 };
            var codeHash = new Sha3KeccackHashProvider().ComputeHash(code);
            var codeStore = new InMemoryStateStore();

            var scheduler = new CodeServingScheduler();
            var healer = new TrieHealer(scheduler, storage, NullLogger.Instance, codeStore: codeStore)
            {
                NoPeerFailureBackoff = TimeSpan.FromMilliseconds(1),
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await healer.HealAsync(
                rootHash, seedCodeHeal: new[] { codeHash }, ct: cts.Token);

            Assert.False(result.Matched, "an un-served seeded code cannot converge");
            Assert.Null(await codeStore.GetCodeAsync(codeHash));
        }


        private static byte[] SeedSingleLeafTrie(InMemoryContentNodeStore storage)
        {
            var trie = new PatriciaTrie(storage);
            trie.Put(new byte[] { 0xAA }, new byte[] { 0x42 });
            trie.SaveNodesToStorage();
            return trie.Root.GetHash();
        }

        private sealed class CodeServingScheduler : IFetchRequestScheduler
        {
            public readonly Dictionary<byte[], byte[]> Codes = new(ByteArrayComparer.Current);
            public int ByteCodeCalls;
            public int ServeAfterCalls;

            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
            {
                int call = Interlocked.Increment(ref ByteCodeCalls);
                var codes = new List<byte[]>();
                if (call > ServeAfterCalls)
                {
                    foreach (var h in codeHashes)
                        if (Codes.TryGetValue(h, out var c)) codes.Add(c);
                }
                return Task.FromResult(new ByteCodesMessage { RequestId = 1, Codes = codes });
            }

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                var msg = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
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
        }
    }
}
