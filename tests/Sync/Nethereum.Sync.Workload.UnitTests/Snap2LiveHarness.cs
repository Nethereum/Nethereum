using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Chain.TestData;
using Nethereum.Contracts;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal static class Snap2LiveHarness
    {
        public const string Collection = "snap2-live";
        public const int TestRootRefreshIntervalMs = 300;
        public const int PrefundedEoas = 25_000;
        public const int SnapResponseCapBytes = 256;

        public sealed class LiveAdvancingTipSource : ICanonicalStateRootSource
        {
            private readonly WireServerNode _server;
            public LiveAdvancingTipSource(WireServerNode server) => _server = server;
            public string Name => "BalHealLiveTip";

            public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                var height = (ulong)await _server.Bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
                var header = await _server.Bundle.Blocks.GetByNumberAsync(height).ConfigureAwait(false);
                var hash = await _server.Bundle.Blocks.GetHashByNumberAsync(height).ConfigureAwait(false);
                return new CanonicalTip { BlockNumber = height, BlockHash = hash, StateRoot = header.StateRoot };
            }

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult(((byte[])null, (byte[])null));
        }

        public sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ConcurrentQueue<string> _messages = new();
            private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
            public IReadOnlyCollection<string> Messages => _messages;
            public Action<string> OnMessage { get; set; }
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel l) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel l, Microsoft.Extensions.Logging.EventId e,
                TState s, Exception ex, Func<TState, Exception, string> f)
            {
                var message = f(s, ex);
                _messages.Enqueue($"[{_sw.Elapsed.TotalSeconds:0.00}s] " + message + (ex != null ? " " + ex.Message : ""));
                OnMessage?.Invoke(message);
            }

            public int Count(string fragment) => _messages.Count(m => m.Contains(fragment, StringComparison.Ordinal));
        }

        public sealed class Server
        {
            public InProcessSequencerDriver Sequencer { get; init; }
            public WireServerNode Node { get; init; }
            public string Token { get; init; }
            public int[] NextChurnStart { get; } = new int[1];

            public Task SealAsync(int count) => SealBlocksAsync(Sequencer, Node, Token, count, NextChurnStart);

            public async Task<ulong> HeightAsync() => (ulong)await Node.Bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
        }

        public static async Task SealBlocksAsync(
            InProcessSequencerDriver sequencer, WireServerNode server, string token, int count, int[] nextChurnStart)
        {
            for (var i = 0; i < count; i++)
            {
                var startIndex = nextChurnStart[0] % 6;
                nextChurnStart[0]++;
                var churn = new ChurnFunction
                {
                    StartIndex = startIndex,
                    Count = 3,
                    Amount = 1,
                };
                sequencer.QueueCall(sequencer.Accounts.All[0], token, churn.GetCallData());
                await sequencer.ProduceBlockAsync().ConfigureAwait(false);
            }
            await server.ImportPendingProducedBlocksAsync().ConfigureAwait(false);
        }

        public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline) { if (condition()) return true; await Task.Delay(50); }
            return condition();
        }

        private static string[] SyntheticEoaAddresses()
            => Enumerable.Range(0, PrefundedEoas).Select(i => "0xf" + i.ToString("x").PadLeft(39, '0')).ToArray();

        public static async Task<Server> StartServerWithInitialGapAsync(int preFollowerBlocks, bool advertiseSnap2 = true)
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(
                generatedAccounts: 4, extraPrefunded: SyntheticEoaAddresses(), hardfork: "amsterdam");

            var token = sequencer.QueueDeploy(sequencer.Accounts.All[0], LoadTestToken.Bytecode.HexToByteArray());
            await sequencer.ProduceBlockAsync();

            var airdrop = new AirdropFunction { StartIndex = 0, Count = 10, Amount = 10_000_000 };
            sequencer.QueueCall(sequencer.Accounts.All[0], token, airdrop.GetCallData());
            await sequencer.ProduceBlockAsync();

            var node = await WireServerNode.StartAsync(sequencer, snapResponseLimit: SnapResponseCapBytes, advertiseSnap2: advertiseSnap2);
            var server = new Server { Sequencer = sequencer, Node = node, Token = token };
            await server.SealAsync(preFollowerBlocks);
            return server;
        }

        public static async Task<(PeerPoolManager Pool, FetchRequestScheduler Scheduler)> ConnectAsync(WireServerNode server, bool advertiseSnap2 = true)
        {
            var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId, advertiseSnap2: advertiseSnap2),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0),
                trustedDialKeys: new[] { server.Enode });
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => !advertiseSnap2 || p.SupportsSnap2), TimeSpan.FromSeconds(30)),
                advertiseSnap2 ? "no snap/2 peer" : "no peer");
            return (pool, new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions()));
        }

        public static (RocksDbManager Manager, RocksDbChainStoreBundle Bundle) OpenFollowerBundle(string dbPath)
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
            return (manager, RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive));
        }

        public static SyncNode Follower(IChainStoreBundle bundle, InProcessSequencerDriver sequencer, CapturingLogger log, IPeerPool pool, IFetchRequestScheduler scheduler)
            => new SyncNode(bundle, new FixedChainActivations(HardforkNames.Parse(sequencer.Hardfork)), log, pool, scheduler);

        public static SnapSyncOrchestratorOptions Snap2Options(SnapSyncMetrics metrics, bool balHealEnabled = true) => new()
        {
            UseBackwardSkeleton = true,
            PivotStaleDistanceBlocks = 15,
            RootRefreshIntervalMs = TestRootRefreshIntervalMs,
            Metrics = metrics,
            BalHealEnabled = balHealEnabled,
        };

        public static long CountTrieRows(RocksDbManager manager)
        {
            long rows = 0;
            foreach (var cf in new[] { RocksDbManager.CF_TRIE_NODES, RocksDbManager.CF_STATE_TRIE_ACCOUNT, RocksDbManager.CF_STATE_TRIE_STORAGE })
            {
                using var iterator = manager.CreateIterator(cf);
                for (iterator.SeekToFirst(); iterator.Valid(); iterator.Next()) rows++;
            }
            return rows;
        }

        public static string GeneratedRoot(CapturingLogger log)
            => log.Messages.Where(m => m.Contains("snap.generate.done", StringComparison.Ordinal))
                .Select(m => m.Substring(m.IndexOf("root=0x", StringComparison.Ordinal) + "root=0x".Length, 64))
                .LastOrDefault();

        public static async Task AssertFlatStateCertifiedAsync(RocksDbChainStoreBundle bundle, RocksDbManager manager, byte[] stateRoot)
        {
            var verify = await ((IFlatStateReconciler)bundle).VerifyFlatStateAsync(stateRoot, _ => { }, CancellationToken.None);
            Assert.Equal(0L, verify.TotalRepairs);

            var flat = (ISnapFlatStateWriter)bundle.State;
            var contracts = 0;
            using var iterator = manager.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            for (iterator.SeekToFirst(); iterator.Valid(); iterator.Next())
            {
                var key = iterator.Key();
                if (key.Length != 32) continue;
                var account = await flat.GetAccountByHashAsync(key);
                if (account?.CodeHash == null || ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH)) continue;
                contracts++;
                var code = await bundle.State.GetCodeAsync(account.CodeHash);
                Assert.True(code is { Length: > 0 }, $"flat account 0x{key.ToHex()} has no code for 0x{account.CodeHash.ToHex()}");
            }
            Assert.True(contracts > 0, "no contract account in flat state — the code-presence check is vacuous");
        }
    }

    internal sealed class ControlledSnapScheduler : IFetchRequestScheduler
    {
        private readonly IFetchRequestScheduler _inner;
        private readonly bool _holdAWhaleAcrossAMove;
        private readonly ConcurrentDictionary<string, int> _pagesAtFirstRoot = new();
        private readonly ConcurrentQueue<string> _whaleRoots = new();
        private byte[] _firstRoot;
        private string _whale;
        private int _trieNodeRequests;
        private volatile bool _paused;

        public ControlledSnapScheduler(IFetchRequestScheduler inner, bool holdAWhaleAcrossAMove = false)
        {
            _inner = inner;
            _holdAWhaleAcrossAMove = holdAWhaleAcrossAMove;
        }

        public int TrieNodeRequests => Volatile.Read(ref _trieNodeRequests);

        public string Whale => Volatile.Read(ref _whale);

        public IReadOnlyCollection<string> WhaleRoots => _whaleRoots.Distinct().ToList();

        public void PauseStateRequests() => _paused = true;

        public void ResumeStateRequests() => _paused = false;

        public sealed class SimulatedCrashException : Exception
        {
            public SimulatedCrashException() : base("simulated crash: the node died with Phase 2 in flight") { }
        }

        private volatile bool _crashed;
        private byte[] _crashAfterPagesOf;
        private int _crashAfterPages;
        private int _pagesOfCrashOwner;
        private volatile bool _holdTrieNodes;

        public bool Crashed => _crashed;

        public bool Paused => _paused;

        public int PagesOfCrashOwner => Volatile.Read(ref _pagesOfCrashOwner);

        public void CrashStateRequests() => _crashed = true;

        public void CrashAfterStoragePagesOf(byte[] accountHash, int pages)
        {
            _crashAfterPages = pages;
            Volatile.Write(ref _crashAfterPagesOf, accountHash);
        }

        public void Recover()
        {
            Volatile.Write(ref _crashAfterPagesOf, null);
            _crashed = false;
        }

        private readonly ConcurrentDictionary<string, int> _trieNodeStorageOwners = new();

        public IReadOnlyCollection<string> TrieNodeStorageOwners => _trieNodeStorageOwners.Keys.ToList();

        private void RecordTrieNodeOwners(List<List<byte[]>> paths)
        {
            foreach (var path in paths.Where(p => p.Count > 1))
                _trieNodeStorageOwners.AddOrUpdate(path[0].ToHex(), 1, (_, n) => n + 1);
        }

        public void HoldTrieNodeRequests() => _holdTrieNodes = true;

        public void ReleaseTrieNodeRequests() => _holdTrieNodes = false;

        private async Task WaitWhilePausedAsync(CancellationToken ct)
        {
            while (_paused) await Task.Delay(50, ct).ConfigureAwait(false);
            if (_crashed) throw new SimulatedCrashException();
        }

        private void RecordCrashOwnerPage(List<byte[]> accountHashes)
        {
            var owner = Volatile.Read(ref _crashAfterPagesOf);
            if (owner == null || accountHashes.Count != 1 || !ByteUtil.AreEqual(accountHashes[0], owner)) return;
            if (Interlocked.Increment(ref _pagesOfCrashOwner) >= _crashAfterPages) _crashed = true;
        }

        private async Task WaitWhileTrieNodesHeldAsync(CancellationToken ct)
        {
            while (_holdTrieNodes) await Task.Delay(50, ct).ConfigureAwait(false);
        }

        private async Task HoldIfWhaleSpansAsync(byte[] stateRoot, CancellationToken ct)
        {
            await WaitWhilePausedAsync(ct).ConfigureAwait(false);
            if (!_holdAWhaleAcrossAMove) return;
            Interlocked.CompareExchange(ref _firstRoot, stateRoot, null);
            if (Whale != null && ByteUtil.AreEqual(stateRoot, Volatile.Read(ref _firstRoot)))
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }

        private void RecordStoragePage(byte[] stateRoot, List<byte[]> accountHashes)
        {
            if (!_holdAWhaleAcrossAMove) return;
            var atFirstRoot = ByteUtil.AreEqual(stateRoot, Volatile.Read(ref _firstRoot));
            foreach (var owner in accountHashes.Select(h => h.ToHex()))
            {
                if (atFirstRoot && _pagesAtFirstRoot.AddOrUpdate(owner, 1, (_, n) => n + 1) == 2)
                    Interlocked.CompareExchange(ref _whale, owner, null);
                if (owner == Whale) _whaleRoots.Enqueue(stateRoot.ToHex());
            }
        }

        public void OnTargetRootChanged() => _inner.OnTargetRootChanged();

        public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => _inner.FetchHeadersAsync(startBlock, limit, ct, reverse);

        public Task<(List<BlockHeader> Headers, Guid PeerId)> FetchHeadersWithPeerAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => _inner.FetchHeadersWithPeerAsync(startBlock, limit, ct, reverse);

        public void QuarantineHeaderPeer(Guid peerId) => _inner.QuarantineHeaderPeer(peerId);

        public Task<List<BlockHeader>> FetchHeadersByHashAsync(byte[] startHash, ulong limit, CancellationToken ct)
            => _inner.FetchHeadersByHashAsync(startHash, limit, ct);

        public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => _inner.FetchBodiesAsync(blockHashes, ct);

        public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
            => _inner.FetchBodiesAsync(blockHashes, excludePeers, ct);

        public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => _inner.FetchReceiptsAsync(blockHashes, ct);

        public async Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            await WaitWhilePausedAsync(ct).ConfigureAwait(false);
            return await _inner.FetchAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, ct).ConfigureAwait(false);
        }

        public async Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, Func<AccountRangeMessage, bool> verifyResponse, CancellationToken ct)
        {
            await WaitWhilePausedAsync(ct).ConfigureAwait(false);
            return await _inner.FetchAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, verifyResponse, ct).ConfigureAwait(false);
        }

        public async Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            await HoldIfWhaleSpansAsync(stateRoot, ct).ConfigureAwait(false);
            var response = await _inner.FetchStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, ct).ConfigureAwait(false);
            RecordStoragePage(stateRoot, accountHashes);
            RecordCrashOwnerPage(accountHashes);
            return response;
        }

        public async Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct)
        {
            await HoldIfWhaleSpansAsync(stateRoot, ct).ConfigureAwait(false);
            var response = await _inner.FetchStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, verifyResponse, ct).ConfigureAwait(false);
            RecordStoragePage(stateRoot, accountHashes);
            RecordCrashOwnerPage(accountHashes);
            return response;
        }

        public async Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
        {
            await WaitWhilePausedAsync(ct).ConfigureAwait(false);
            return await _inner.FetchByteCodesAsync(codeHashes, responseBytes, ct).ConfigureAwait(false);
        }

        public async Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, Func<ByteCodesMessage, bool> verifyResponse, CancellationToken ct)
        {
            await WaitWhilePausedAsync(ct).ConfigureAwait(false);
            return await _inner.FetchByteCodesAsync(codeHashes, responseBytes, verifyResponse, ct).ConfigureAwait(false);
        }

        public async Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
        {
            Interlocked.Increment(ref _trieNodeRequests);
            RecordTrieNodeOwners(paths);
            await WaitWhileTrieNodesHeldAsync(ct).ConfigureAwait(false);
            return await _inner.FetchTrieNodesAsync(stateRoot, paths, responseBytes, ct).ConfigureAwait(false);
        }

        public async Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, Func<TrieNodesMessage, bool> verifyResponse, CancellationToken ct)
        {
            Interlocked.Increment(ref _trieNodeRequests);
            RecordTrieNodeOwners(paths);
            await WaitWhileTrieNodesHeldAsync(ct).ConfigureAwait(false);
            return await _inner.FetchTrieNodesAsync(stateRoot, paths, responseBytes, verifyResponse, ct).ConfigureAwait(false);
        }

        public bool IsSnapStateServing(IEthPeer peer) => _inner.IsSnapStateServing(peer);
    }
}
