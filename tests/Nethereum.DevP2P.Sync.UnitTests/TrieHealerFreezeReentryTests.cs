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
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TrieHealerFreezeReentryTests
    {
        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static byte[] Filled(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        private sealed class RootAwareScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly byte[] _servableRoot;
            public int StaleRootCalls;
            public int ServableRootCalls;

            public RootAwareScheduler(PatriciaSnapRequestHandler handler, byte[] servableRoot)
            {
                _handler = handler;
                _servableRoot = servableRoot;
            }

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                {
                    Interlocked.Increment(ref StaleRootCalls);
                    var empty = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                    for (int i = 0; i < paths.Count; i++) empty.Nodes.Add(Array.Empty<byte>());
                    return empty;
                }
                Interlocked.Increment(ref ServableRootCalls);
                return await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    Paths = paths,
                    ResponseBytes = responseBytes,
                });
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
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

        private sealed class PathCountingScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            public int TotalPathsRequested;

            public PathCountingScheduler(PatriciaSnapRequestHandler handler) => _handler = handler;

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Add(ref TotalPathsRequested, paths.Count);
                return await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    Paths = paths,
                    ResponseBytes = responseBytes,
                });
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
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

        [Fact]
        public async Task RootHeal_PivotGoesStale_FreezesAndSignalsRetarget_ThenReentryConverges()
        {
            var serverStore = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(serverStore);
            trie.Put(Acc(0x10), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x01 }));
            trie.Put(Acc(0x90), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x02 }));
            trie.SaveDirtyNodesToStorage();
            var freshRoot = trie.Root.GetHash();

            var handler = new PatriciaSnapRequestHandler(serverStore, new NullBytecodes());
            var scheduler = new RootAwareScheduler(handler, servableRoot: freshRoot);

            var staleRoot = Filled(0xCD);

            var clientStore = new InMemoryContentNodeStore();
            var healer = new TrieHealer(scheduler, clientStore, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((freshRoot, 500UL));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var firstResult = await healer.HealAsync(staleRoot, ct: cts.Token);

            Assert.False(firstResult.Matched,
                $"the stale-root cycle must not silently converge in place (staleCalls={scheduler.StaleRootCalls}, servableCalls={scheduler.ServableRootCalls})");
            Assert.True(firstResult.NeedsRetarget);
            Assert.Equal(staleRoot, firstResult.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.Equal(freshRoot, firstResult.RetargetRoot, ByteArrayComparer.Current);
            Assert.True(scheduler.StaleRootCalls > 0, "the pinned root must have been tried (and starved) before the staleness was detected");
            Assert.False(clientStore.ContainsKey(freshRoot));

            var healer2 = new TrieHealer(scheduler, clientStore, NullLogger.Instance);
            healer2.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((freshRoot, 500UL));
            var secondResult = await healer2.HealAsync(firstResult.RetargetRoot, pivotBlock: firstResult.RetargetBlock, ct: cts.Token);

            Assert.True(secondResult.Matched, "the re-entered cycle against the fresh root must converge");
            Assert.True(clientStore.ContainsKey(freshRoot));
        }

        [Fact]
        public async Task RootHeal_Reentry_SkipsAlreadyCommittedNodes_ContentAddressedReuse()
        {
            var serverStore = new InMemoryContentNodeStore();
            var keyX = Acc(0x10);
            var keyY = Acc(0x90);
            var valueX1 = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x01 });
            var valueX2 = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x02 });
            var valueY = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x09 });

            var trieA = new PatriciaTrie(serverStore);
            trieA.Put(keyX, valueX1);
            trieA.Put(keyY, valueY);
            trieA.SaveDirtyNodesToStorage();
            var rootA = trieA.Root.GetHash();

            var trieB = new PatriciaTrie(serverStore);
            trieB.Put(keyX, valueX2);
            trieB.Put(keyY, valueY);
            trieB.SaveDirtyNodesToStorage();
            var rootB = trieB.Root.GetHash();
            Assert.False(ByteArrayComparer.Current.Equals(rootA, rootB),
                "test fixture assumption: X changing must change the root");

            var handler = new PatriciaSnapRequestHandler(serverStore, new NullBytecodes());

            var freshClientStore = new InMemoryContentNodeStore();
            var freshScheduler = new PathCountingScheduler(handler);
            var freshHealer = new TrieHealer(freshScheduler, freshClientStore, NullLogger.Instance);
            var freshResult = await freshHealer.HealAsync(rootB, ct: CancellationToken.None);
            Assert.True(freshResult.Matched);
            var freshWalkPaths = freshScheduler.TotalPathsRequested;

            var clientStore = new InMemoryContentNodeStore();
            var scheduler1 = new PathCountingScheduler(handler);
            var healer1 = new TrieHealer(scheduler1, clientStore, NullLogger.Instance);
            var result1 = await healer1.HealAsync(rootA, ct: CancellationToken.None);
            Assert.True(result1.Matched);

            var scheduler2 = new PathCountingScheduler(handler);
            var healer2 = new TrieHealer(scheduler2, clientStore, NullLogger.Instance);
            var result2 = await healer2.HealAsync(rootB, ct: CancellationToken.None);
            Assert.True(result2.Matched);

            Assert.True(scheduler2.TotalPathsRequested < freshWalkPaths,
                $"a re-entry cycle against a fresh root sharing content with the PRIOR cycle must request FEWER " +
                $"node paths than a full fresh walk (content-addressed reuse — 'no progress lost') — " +
                $"got {scheduler2.TotalPathsRequested} vs fresh-walk baseline {freshWalkPaths}");
        }
    }
}
