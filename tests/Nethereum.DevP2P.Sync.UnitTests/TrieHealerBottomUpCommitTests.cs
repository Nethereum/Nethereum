using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
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
    public class TrieHealerBottomUpCommitTests
    {
        [Fact]
        public async Task InterruptedSubtree_ParentNotPersisted_ResumeDescendsAndCompletes()
        {
            var remote = BuildRemoteTrie(out var rootHash, out var blobs, out var hashes);

            var local = new InMemoryContentNodeStore();
            var script1 = new FakeScriptScheduler(new[]
            {
                new[] { blobs["R"] },
                new[] { blobs["C"], blobs["L3"] },
                new[] { blobs["A"] },
            });
            script1.CancelAfterCalls = 3;
            var healer1 = new TrieHealer(script1, local, NullLogger.Instance);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => healer1.HealAsync(rootHash, ct: script1.Cts.Token));

            Assert.NotNull(local.Get(hashes["A"]));
            Assert.NotNull(local.Get(hashes["L3"]));
            Assert.Null(local.Get(hashes["C"]));
            Assert.Null(local.Get(hashes["R"]));

            var script2 = new FakeScriptScheduler(new[]
            {
                new[] { blobs["R"] },
                new[] { blobs["C"] },
                new[] { blobs["B"] },
            });
            var healer2 = new TrieHealer(script2, local, NullLogger.Instance);
            var result = await healer2.HealAsync(rootHash, ct: CancellationToken.None);

            Assert.True(result.Matched, "resume must converge");
            Assert.NotNull(local.Get(hashes["B"]));
            Assert.NotNull(local.Get(hashes["C"]));
            Assert.NotNull(local.Get(hashes["R"]));
            Assert.True(script2.FetchCalls <= 6, $"resume should be a short walk; calls={script2.FetchCalls}");
        }

        [Fact]
        public async Task SeededHeal_MatchingRootButMissingChildren_DescendsAndCompletes()
        {
            var remote = BuildRemoteTrie(out var rootHash, out var blobs, out var hashes);
            var local = new InMemoryContentNodeStore();
            var owner = Hash32(0xFA);

            local.Put(rootHash, blobs["R"]);
            var script = new FakeScriptScheduler(new[]
            {
                new[] { blobs["R"] },
                new[] { blobs["C"], blobs["L3"] },
                new[] { blobs["A"], blobs["B"] },
            });
            var healer = new TrieHealer(script, local, NullLogger.Instance);

            var result = await healer.HealAsync(
                Hash32(0x99),
                seedStorageHeal: new[] { (owner, rootHash) },
                ct: CancellationToken.None);

            Assert.True(result.Matched, "seeded heal must not treat root presence as deep completion");
            Assert.NotNull(local.Get(hashes["A"]));
            Assert.NotNull(local.Get(hashes["B"]));
            Assert.NotNull(local.Get(hashes["C"]));
            Assert.NotNull(local.Get(hashes["L3"]));
            Assert.NotNull(local.Get(hashes["R"]));
            Assert.True(script.FetchCalls > 0, "matching root still requires a forced-deep fetch walk");
        }
        [Fact]
        public async Task FetchFailures_BackOffAndReachStallExit_NotMaxRounds()
        {
            var local = new InMemoryContentNodeStore();
            var targetRoot = new byte[32];
            for (int i = 0; i < 32; i++) targetRoot[i] = 0xCD;

            var scheduler = new AlwaysThrowScheduler();
            var healer = new TrieHealer(scheduler, local, NullLogger.Instance)
            {
                FetchFailureBackoff = TimeSpan.FromMilliseconds(1),
                NoPeerFailureBackoff = TimeSpan.FromMilliseconds(1),
            };
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((targetRoot, 0UL));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await healer.HealAsync(targetRoot, ct: cts.Token);

            Assert.False(result.Matched);
            Assert.True(scheduler.Calls <= 800,
                $"fetch failures must reach the stall exit, not MaxRounds; calls={scheduler.Calls}");
        }


        private static byte[] Hash32(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
        private static InMemoryContentNodeStore BuildRemoteTrie(
            out byte[] rootHash, out Dictionary<string, byte[]> blobs, out Dictionary<string, byte[]> hashes)
        {
            var remote = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(remote);

            byte[] Key(byte first) { var k = new byte[32]; k[0] = first; for (int i = 1; i < 32; i++) k[i] = (byte)i; return k; }
            byte[] Value(byte tag) { var v = new byte[40]; for (int i = 0; i < 40; i++) v[i] = tag; return v; }

            trie.Put(Key(0x0A), Value(1));
            trie.Put(Key(0x0B), Value(2));
            trie.Put(Key(0x1C), Value(3));
            trie.SaveNodesToStorage();

            rootHash = trie.Root.GetHash();
            var rootBlob = remote.Get(rootHash);

            var decoder = new NodeDecoder();
            var wrapped = ContentAddressedNodeStore.Wrap(remote);
            var rootNode = (BranchNode)decoder.DecodeFromRlpData(rootBlob, null, new byte[0], decodeHashNodes: false, wrapped);
            var cHash = ((HashNode)rootNode.Children[0]).Hash;
            var l3Hash = ((HashNode)rootNode.Children[1]).Hash;
            var cBlob = remote.Get(cHash);
            var cNode = (BranchNode)decoder.DecodeFromRlpData(cBlob, null, new byte[0], decodeHashNodes: false, wrapped);
            var aHash = ((HashNode)cNode.Children[0x0A]).Hash;
            var bHash = ((HashNode)cNode.Children[0x0B]).Hash;

            hashes = new Dictionary<string, byte[]>
            {
                ["R"] = rootHash, ["C"] = cHash, ["L3"] = l3Hash, ["A"] = aHash, ["B"] = bHash,
            };
            blobs = new Dictionary<string, byte[]>
            {
                ["R"] = rootBlob, ["C"] = cBlob, ["L3"] = remote.Get(l3Hash),
                ["A"] = remote.Get(aHash), ["B"] = remote.Get(bHash),
            };
            return remote;
        }

        private sealed class FakeScriptScheduler : IFetchRequestScheduler
        {
            private readonly Queue<byte[][]> _script;
            public int FetchCalls;
            public int CancelAfterCalls = int.MaxValue;
            public CancellationTokenSource Cts { get; } = new();

            public FakeScriptScheduler(IEnumerable<byte[][]> script) => _script = new Queue<byte[][]>(script);

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var calls = Interlocked.Increment(ref FetchCalls);
                var msg = new TrieNodesMessage { RequestId = (ulong)calls, Nodes = new List<byte[]>() };
                if (_script.Count > 0)
                    msg.Nodes.AddRange(_script.Dequeue());
                if (calls >= CancelAfterCalls) Cts.Cancel();
                return Task.FromResult(msg);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class AlwaysThrowScheduler : IFetchRequestScheduler
        {
            public int Calls;

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref Calls);
                throw new InvalidOperationException("simulated dead fetch path");
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
