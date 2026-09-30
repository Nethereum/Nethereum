using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TrieHealerSeededRotationTests
    {
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider Hp = new Sha3KeccackHashProvider();

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class WindowedScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly byte[] _servableRoot;
            public int StaleTrieNodeCalls;
            public int AccountRangeCalls;

            public WindowedScheduler(PatriciaSnapRequestHandler handler, byte[] servableRoot)
            {
                _handler = handler;
                _servableRoot = servableRoot;
            }

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                {
                    Interlocked.Increment(ref StaleTrieNodeCalls);
                    var empty = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                    for (int i = 0; i < paths.Count; i++) empty.Nodes.Add(Array.Empty<byte>());
                    return empty;
                }
                return await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    Paths = paths,
                    ResponseBytes = responseBytes,
                });
            }

            public async Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref AccountRangeCalls);
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                    throw new FetchRequestFailedException("stale root not servable", null);
                return await _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    StartingHash = startingHash,
                    LimitHash = limitHash,
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
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private sealed class PartiallyFailingWindowedScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly byte[] _servableRoot;
            private readonly byte[] _failingAccountHash;
            public int AccountRangeCalls;
            public int FailingAccountRangeCalls;

            public PartiallyFailingWindowedScheduler(PatriciaSnapRequestHandler handler, byte[] servableRoot, byte[] failingAccountHash)
            {
                _handler = handler;
                _servableRoot = servableRoot;
                _failingAccountHash = failingAccountHash;
            }

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                {
                    var stale = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                    for (int i = 0; i < paths.Count; i++) stale.Nodes.Add(Array.Empty<byte>());
                    return stale;
                }

                var nodes = new byte[paths.Count][];
                var servablePaths = new List<List<byte[]>>();
                var servableIndexes = new List<int>();
                for (int i = 0; i < paths.Count; i++)
                {
                    var pathset = paths[i];
                    bool isFailingAccount = pathset.Count > 1 && ByteUtil.AreEqual(pathset[0], _failingAccountHash);
                    if (isFailingAccount) nodes[i] = Array.Empty<byte>();
                    else { servablePaths.Add(pathset); servableIndexes.Add(i); }
                }

                if (servablePaths.Count > 0)
                {
                    var resp = await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                    {
                        RequestId = 1,
                        RootHash = stateRoot,
                        Paths = servablePaths,
                        ResponseBytes = responseBytes,
                    });
                    for (int k = 0; k < servableIndexes.Count && k < resp.Nodes.Count; k++)
                        nodes[servableIndexes[k]] = resp.Nodes[k];
                }

                var result = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>(paths.Count) };
                for (int i = 0; i < paths.Count; i++) result.Nodes.Add(nodes[i] ?? Array.Empty<byte>());
                return result;
            }

            public async Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            {
                await Task.Delay(20, ct).ConfigureAwait(false);
                Interlocked.Increment(ref AccountRangeCalls);
                if (ByteUtil.AreEqual(startingHash, _failingAccountHash))
                {
                    Interlocked.Increment(ref FailingAccountRangeCalls);
                    throw new FetchRequestFailedException("transient peer failure re-resolving seed", null);
                }
                if (!ByteUtil.AreEqual(stateRoot, _servableRoot))
                    throw new FetchRequestFailedException("stale root not servable", null);
                return await _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    StartingHash = startingHash,
                    LimitHash = startingHash,
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
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private static (InMemoryContentNodeStore Server, byte[] StateRoot, byte[] AccountHash, byte[] StorageRoot)
            BuildServerState()
        {
            var server = new InMemoryContentNodeStore();

            var storageTrie = new PatriciaTrie(server, Hp);
            for (byte i = 1; i <= 3; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { i });
                storageTrie.Put(slotHash, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(0xA0 + i) }));
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var accountHash = Keccak.CalculateHash(new byte[] { 0x01, 0xAA });
            var account = new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            var accountTrie = new PatriciaTrie(server, Hp);
            accountTrie.Put(accountHash, account);
            accountTrie.SaveDirtyNodesToStorage();

            return (server, accountTrie.Root.GetHash(), accountHash, storageRoot);
        }

        private static (InMemoryContentNodeStore Server, byte[] StateRoot,
            byte[] AccountHashA, byte[] StorageRootA, byte[] AccountHashB, byte[] StorageRootB)
            BuildTwoAccountServerState()
        {
            var server = new InMemoryContentNodeStore();

            byte[] BuildStorageRoot(byte seed)
            {
                var storageTrie = new PatriciaTrie(server, Hp);
                for (byte i = 1; i <= 3; i++)
                {
                    var slotHash = Keccak.CalculateHash(new byte[] { seed, i });
                    storageTrie.Put(slotHash, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(0xA0 + i) }));
                }
                storageTrie.SaveDirtyNodesToStorage();
                return storageTrie.Root.GetHash();
            }

            var storageRootA = BuildStorageRoot(0x01);
            var storageRootB = BuildStorageRoot(0x02);

            var accountHashA = Keccak.CalculateHash(new byte[] { 0x01, 0xAA });
            var accountHashB = Keccak.CalculateHash(new byte[] { 0x02, 0xBB });

            var encoder = new AccountEncoder();
            var accountA = encoder.Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRootA,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            var accountB = encoder.Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)2000UL,
                StateRoot = storageRootB,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });

            var accountTrie = new PatriciaTrie(server, Hp);
            accountTrie.Put(accountHashA, accountA);
            accountTrie.Put(accountHashB, accountB);
            accountTrie.SaveDirtyNodesToStorage();

            return (server, accountTrie.Root.GetHash(), accountHashA, storageRootA, accountHashB, storageRootB);
        }


        [Fact]
        public async Task Given_SeededRotationBatchWithOneFailingSeed_When_RotationCompletes_Then_SuccessfullyResolvedSeedsAreKept()
        {
            var (server, stateRoot, accountHashA, storageRootA, accountHashB, storageRootB) = BuildTwoAccountServerState();
            var handler = new PatriciaSnapRequestHandler(server, new NullBytecodes());
            var scheduler = new PartiallyFailingWindowedScheduler(handler, servableRoot: stateRoot, failingAccountHash: accountHashB);

            var staleRoot = new byte[32];
            for (int i = 0; i < 32; i++) staleRoot[i] = 0xCD;

            var sink = new InMemoryContentNodeStore();
            var healer = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var firstResult = await healer.HealAsync(
                staleRoot,
                seedStorageHeal: new[] { (accountHashA, storageRootA), (accountHashB, storageRootB) },
                pivotBlock: 0UL,
                ct: cts.Token);

            Assert.False(firstResult.Matched);
            Assert.True(firstResult.NeedsRetarget);
            Assert.Equal(stateRoot, firstResult.RetargetRoot, ByteArrayComparer.Current);
            Assert.True(scheduler.AccountRangeCalls >= 2,
                "both seeds must have their re-resolve attempted — the failure must not prevent the OTHER seed from being tried");
            Assert.True(scheduler.FailingAccountRangeCalls >= 1, "the failing seed's re-resolve must actually have been exercised");

            Assert.NotNull(firstResult.RetargetSeeds);
            Assert.Contains(firstResult.RetargetSeeds, s => ByteArrayComparer.Current.Equals(s.AccountHash, accountHashA)
                && ByteArrayComparer.Current.Equals(s.StorageRoot, storageRootA));
            Assert.Contains(firstResult.RetargetSeeds, s => ByteArrayComparer.Current.Equals(s.AccountHash, accountHashB)
                && ByteArrayComparer.Current.Equals(s.StorageRoot, storageRootB));

            var healer2 = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer2.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));
            var secondResult = await healer2.HealAsync(
                firstResult.RetargetRoot, seedStorageHeal: firstResult.RetargetSeeds,
                pivotBlock: firstResult.RetargetBlock, ct: cts.Token);

            Assert.True(sink.ContainsKey(storageRootA),
                "accountA's successfully re-resolved storage must be healed and kept, not discarded because accountB's fetch failed");
            Assert.False(sink.ContainsKey(storageRootB),
                "accountB's seed must not be silently treated as resolved when its fetch never actually succeeded");
            Assert.False(secondResult.Matched,
                "one seed remains genuinely unresolved, so the overall heal must not report full convergence");
        }

        [Fact]
        public async Task SeededHeal_StaleRoot_FreezesThenReentersAndCompletesRepair()
        {
            var (server, stateRoot, accountHash, storageRoot) = BuildServerState();
            var handler = new PatriciaSnapRequestHandler(server, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: stateRoot);

            var staleRoot = new byte[32];
            for (int i = 0; i < 32; i++) staleRoot[i] = 0xCD;

            var sink = new InMemoryContentNodeStore();
            var healer = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var firstResult = await healer.HealAsync(
                staleRoot,
                seedStorageHeal: new[] { (accountHash, storageRoot) },
                pivotBlock: 0UL,
                ct: cts.Token);

            Assert.False(firstResult.Matched,
                $"the stale-root cycle must not silently converge in place (staleCalls={scheduler.StaleTrieNodeCalls}, rangeCalls={scheduler.AccountRangeCalls})");
            Assert.True(firstResult.NeedsRetarget);
            Assert.Equal(staleRoot, firstResult.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.Equal(stateRoot, firstResult.RetargetRoot, ByteArrayComparer.Current);
            Assert.NotNull(firstResult.RetargetSeeds);
            Assert.True(scheduler.AccountRangeCalls >= 1, "detecting the retarget must re-resolve the seed with a proof-verified account fetch");
            Assert.True(scheduler.StaleTrieNodeCalls > 0, "the pinned root must have been tried (and starved) before the staleness was detected");

            var healer2 = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer2.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));
            var secondResult = await healer2.HealAsync(
                firstResult.RetargetRoot, seedStorageHeal: firstResult.RetargetSeeds,
                pivotBlock: firstResult.RetargetBlock, ct: cts.Token);

            Assert.True(secondResult.Matched, "the re-entered cycle against the fresh, re-resolved root must converge");
            Assert.True(sink.ContainsKey(storageRoot));
        }

        [Fact]
        public async Task SeededHeal_WipeSeedsFirst_StaleRoot_FreezesThenReentersAndRefetchesFully()
        {
            var (server, stateRoot, accountHash, storageRoot) = BuildServerState();
            var handler = new PatriciaSnapRequestHandler(server, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: stateRoot);

            var staleRoot = new byte[32];
            for (int i = 0; i < 32; i++) staleRoot[i] = 0xCD;

            var sink = new InMemoryContentNodeStore();
            var healer = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var firstResult = await healer.HealAsync(
                staleRoot,
                seedStorageHeal: new[] { (accountHash, storageRoot) },
                pivotBlock: 0UL,
                wipeSeedsFirst: true,
                ct: cts.Token);

            Assert.False(firstResult.Matched);
            Assert.True(firstResult.NeedsRetarget);
            Assert.NotNull(firstResult.RetargetSeeds);
            Assert.True(scheduler.AccountRangeCalls >= 1);

            var healer2 = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer2.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));
            var secondResult = await healer2.HealAsync(
                firstResult.RetargetRoot, seedStorageHeal: firstResult.RetargetSeeds,
                pivotBlock: firstResult.RetargetBlock, ct: cts.Token);

            Assert.True(secondResult.Matched,
                "the production flag combination (wipe + freeze/re-enter) must converge once re-entered");
        }

        [Fact]
        public async Task SeededHeal_AccountGoneAtFreshRoot_DropsSeedAndConvergesOnReentry()
        {
            var (server, stateRoot, _, storageRoot) = BuildServerState();
            var handler = new PatriciaSnapRequestHandler(server, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: stateRoot);

            var staleRoot = new byte[32];
            for (int i = 0; i < 32; i++) staleRoot[i] = 0xCD;

            var goneAccount = Keccak.CalculateHash(new byte[] { 0x77, 0x77 });

            var sink = new InMemoryContentNodeStore();
            var healer = new TrieHealer(scheduler, sink, NullLogger.Instance);
            healer.PivotRefresher = (_, ct) => Task.FromResult<(byte[] Root, ulong Block)?>((stateRoot, 500UL));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var firstResult = await healer.HealAsync(
                staleRoot,
                seedStorageHeal: new[] { (goneAccount, storageRoot) },
                pivotBlock: 0UL,
                ct: cts.Token);

            Assert.False(firstResult.Matched);
            Assert.True(firstResult.NeedsRetarget);
            Assert.NotNull(firstResult.RetargetSeeds);
            Assert.Empty(firstResult.RetargetSeeds);
        }

        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (formatter != null) Entries.Add((logLevel, formatter(state, exception)));
            }
        }

        [Fact]
        public async Task Given_ASeededHealWithNoPivotRefresherWired_When_PinnedToAStaleRoot_Then_ItLogsAnErrorInsteadOfSilentlyNeverRotating()
        {
            var (server, stateRoot, accountHash, storageRoot) = BuildServerState();
            var handler = new PatriciaSnapRequestHandler(server, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: stateRoot);

            var staleRoot = new byte[32];
            for (int i = 0; i < 32; i++) staleRoot[i] = 0xCD;

            var sink = new InMemoryContentNodeStore();
            var logger = new CapturingLogger();
            var healer = new TrieHealer(scheduler, sink, logger);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await healer.HealAsync(
                    staleRoot,
                    seedStorageHeal: new[] { (accountHash, storageRoot) },
                    pivotBlock: 0UL,
                    ct: cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Error && e.Message.Contains("heal.seeded.rotate unavailable"));
        }

        [Fact]
        public async Task Given_ASeededHealsPivotRefresherThrows_When_RotationIsAttempted_Then_ItLogsAWarningAndKeepsTheCurrentRoot()
        {
            var (server, stateRoot, accountHash, storageRoot) = BuildServerState();
            var handler = new PatriciaSnapRequestHandler(server, new NullBytecodes());
            var scheduler = new WindowedScheduler(handler, servableRoot: stateRoot);

            var staleRoot = new byte[32];
            for (int i = 0; i < 32; i++) staleRoot[i] = 0xCD;

            var sink = new InMemoryContentNodeStore();
            var logger = new CapturingLogger();
            var healer = new TrieHealer(scheduler, sink, logger)
            {
                PivotRefresher = (_, _) => throw new InvalidOperationException("refresher backend unreachable"),
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await healer.HealAsync(
                    staleRoot,
                    seedStorageHeal: new[] { (accountHash, storageRoot) },
                    pivotBlock: 0UL,
                    ct: cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Warning && e.Message.Contains("heal.seeded.rotate refresher threw"));
        }
    }
}
