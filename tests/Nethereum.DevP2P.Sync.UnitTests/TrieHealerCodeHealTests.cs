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
using Nethereum.DevP2P.Sync.Serving;

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

            var scheduler = new CodeServingScheduler { TrieNodes = new PatriciaSnapRequestHandler(storage, new NoBytecodes()) };
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

            var scheduler = new CodeServingScheduler { ServeAfterCalls = 2, TrieNodes = new PatriciaSnapRequestHandler(storage, new NoBytecodes()) };
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

            var scheduler = new CodeServingScheduler { TrieNodes = new PatriciaSnapRequestHandler(storage, new NoBytecodes()) };
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

            var scheduler = new CodeServingScheduler { TrieNodes = new PatriciaSnapRequestHandler(storage, new NoBytecodes()) };
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

        [Fact]
        public async Task Given_AContractAccountWhoseCodeNoPeerServes_When_Healing_Then_ItsLeafIsNotWritten()
        {
            var code = new byte[] { 0x60, 0x0B, 0xF3 };
            var codeHash = new Sha3KeccackHashProvider().ComputeHash(code);
            var server = new InMemoryContentNodeStore();
            var accountTrie = new PatriciaTrie(server);
            accountTrie.Put(new Sha3KeccackHashProvider().ComputeHash(new byte[] { 0x0C }), new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = codeHash,
            }));
            accountTrie.SaveNodesToStorage();
            var rootHash = accountTrie.Root.GetHash();

            var scheduler = new CodeServingScheduler { TrieNodes = new PatriciaSnapRequestHandler(server, new NoBytecodes()) };
            var follower = new InMemoryContentNodeStore();
            var healer = new TrieHealer(scheduler, follower, NullLogger.Instance, codeStore: new InMemoryStateStore())
            {
                NoPeerFailureBackoff = TimeSpan.FromMilliseconds(1),
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await healer.HealAsync(rootHash, ct: cts.Token);

            Assert.False(result.Matched, "a contract leaf whose code was never served cannot converge");
            Assert.True(scheduler.ByteCodeCalls >= 1, "the heal never asked for the contract's code");
            Assert.Null(follower.Get(rootHash));
        }

        private sealed class NoBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
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

            public PatriciaSnapRequestHandler TrieNodes;

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                if (TrieNodes != null)
                    return TrieNodes.GetTrieNodesAsync(new GetTrieNodesMessage { RequestId = 1, RootHash = stateRoot, Paths = paths, ResponseBytes = responseBytes });
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
