using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperNeedHealWaitTests
    {
        private const string WaitingForPivot = "snap.phase3.waiting_for_pivot";
        private const string StalledCycle = "snap.phase3.stalled";
        private static readonly TimeSpan WaitStartBound = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PivotPollInterval = TimeSpan.FromSeconds(12);

        private sealed class NullBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class MessageLog : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ConcurrentQueue<string> _messages = new();
            public IReadOnlyCollection<string> Messages => _messages;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
                => _messages.Enqueue(formatter(state, exception));
            public int Count(string fragment) => _messages.Count(m => m.Contains(fragment, StringComparison.Ordinal));

            public async Task<bool> WaitForAsync(string fragment, int count, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (Count(fragment) >= count) return true;
                    await Task.Delay(50);
                }
                return Count(fragment) >= count;
            }
        }

        private sealed class JoinablePool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 1;
            public event EventHandler<IEthPeer> PeerAdded;
            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
            public void Join(IEthPeer peer) => PeerAdded?.Invoke(this, peer);
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class JoiningPeer : IEthPeer
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode => "enode://joining@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 0;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null;
            public event EventHandler<IEthPeer> Disconnected { add { } remove { } }
        }

        private sealed class StateWithoutTrieNodesScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly Func<IEthPeer, bool> _servesSnapState;
            private int _trieNodeRequests;

            public StateWithoutTrieNodesScheduler(PatriciaSnapRequestHandler handler, Func<IEthPeer, bool> servesSnapState)
            {
                _handler = handler;
                _servesSnapState = servesSnapState;
            }

            public int TrieNodeRequests => Volatile.Read(ref _trieNodeRequests);

            public Action OnFirstTrieNodeRequest { get; set; }

            public bool IsSnapStateServing(IEthPeer peer) => _servesSnapState(peer);

            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                if (Interlocked.Increment(ref _trieNodeRequests) == 1) OnFirstTrieNodeRequest?.Invoke();
                return Task.FromResult(new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() });
            }

            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1, RootHash = stateRoot, StartingHash = startingHash, LimitHash = limitHash, ResponseBytes = responseBytes,
                }, ct);

            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => _handler.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = 1, RootHash = stateRoot, AccountHashes = accountHashes, StartingHash = startingHash, LimitHash = limitHash, ResponseBytes = responseBytes,
                }, ct);

            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => _handler.GetByteCodesAsync(new GetByteCodesMessage { RequestId = 1, Hashes = codeHashes, ResponseBytes = responseBytes }, ct);

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotSupportedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotSupportedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotSupportedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotSupportedException();
        }

        private sealed class KeccakSlotBundle : IChainStoreBundle
        {
            private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();
            private readonly KeccakSlotStateStore _state;

            public KeccakSlotBundle() => _state = new KeccakSlotStateStore(_inner.State);

            public IStateStore State => _state;
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }

        private sealed class KeccakSlotStateStore : IStateStore, ISnapFlatStateWriter
        {
            private readonly IStateStore _inner;
            private readonly ConcurrentDictionary<string, byte[]> _slots = new();

            public KeccakSlotStateStore(IStateStore inner) => _inner = inner;

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            {
                _slots[Convert.ToHexString(accountHash) + Convert.ToHexString(slotKeccak)] = value;
                return Task.CompletedTask;
            }

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => ((ISnapFlatStateWriter)_inner).SaveAccountByHashAsync(accountHash, account);
            public Task<Account> GetAccountByHashAsync(byte[] accountHash) => ((ISnapFlatStateWriter)_inner).GetAccountByHashAsync(accountHash);
            public Task DeleteAccountByHashAsync(byte[] accountHash) => ((ISnapFlatStateWriter)_inner).DeleteAccountByHashAsync(accountHash);
            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, System.Numerics.BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, System.Numerics.BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<System.Numerics.BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
        }

        private sealed class NeedHealRun
        {
            public readonly KeccakSlotBundle Bundle = new();
            public readonly StateWithoutTrieNodesScheduler Scheduler;
            public readonly JoinablePool Pool = new();
            public readonly MessageLog Log = new();
            public readonly BlockHeader Pivot;
            public readonly byte[] PivotHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x40 });
            private readonly PatriciaSnapRequestHandler _handler;

            public NeedHealRun(Func<IEthPeer, bool> servesSnapState)
            {
                var keccak = Sha3Keccack.Current;
                var server = new InMemoryContentNodeStore();
                var storage = new PatriciaTrie(server);
                for (byte i = 1; i <= 6; i++)
                    storage.Put(keccak.CalculateHash(new[] { i }), Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(0xC0 + i) }));
                storage.SaveDirtyNodesToStorage();
                var storageRoot = storage.Root.GetHash();

                var contract = keccak.CalculateHash(new byte[] { 0x0C, 0x0C });
                var accounts = new PatriciaTrie(server);
                accounts.Put(contract, new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)1,
                    Balance = (EvmUInt256)1u,
                    StateRoot = storageRoot,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
                for (byte i = 1; i <= 3; i++)
                    accounts.Put(keccak.CalculateHash(new[] { i }), new AccountEncoder().Encode(new Account
                    {
                        Nonce = (EvmUInt256)i,
                        Balance = (EvmUInt256)(i * 10u),
                        StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                        CodeHash = DefaultValues.EMPTY_DATA_HASH,
                    }));
                accounts.SaveDirtyNodesToStorage();
                var stateRoot = accounts.Root.GetHash();

                Pivot = new BlockHeader { BlockNumber = 40, StateRoot = stateRoot };
                _handler = new PatriciaSnapRequestHandler(server, new NullBytecodeStore());
                Scheduler = new StateWithoutTrieNodesScheduler(_handler, servesSnapState);
                Bundle.Metadata.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Phase3Running,
                    PivotBlockNumber = 40UL,
                    PivotBlockHash = PivotHash,
                    HealTargetRoot = stateRoot,
                    Tasks = Array.Empty<SnapSyncAccountTask>(),
                    Counters = SnapSyncCounters.Zero,
                });
                Bundle.Metadata.SaveDeferredHealAccountsBlob(NeedsHealAccountsCodec.Encode(new[]
                {
                    new SnapSyncClient.AccountNeedingHeal(contract, storageRoot),
                }));
            }

            public Task<SnapBootstrapper.Result> RunAsync(CancellationToken ct)
                => SnapBootstrapper.RunAsync(
                    Bundle, new InProcessSnapPeer(_handler), Pivot, PivotHash, Log,
                    new SnapRunOptions
                    {
                        Scheduler = Scheduler,
                        Pool = Pool,
                        RunBackfill = false,
                        AccountConcurrency = 1,
                        PivotRefresher = (_, _) => Task.FromResult<(BlockHeader, byte[])?>(null),
                    },
                    ct);
        }

        private static async Task StopAsync(Task run, CancellationTokenSource cts)
        {
            if (run.IsCompleted) return;
            cts.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
        }

        [Fact]
        public async Task Given_ASeededNeedHealAgainstARootNoPeerServes_When_ThePivotDoesNotAdvance_Then_ItWaitsForANewPivotInsteadOfRecycling()
        {
            var heal = new NeedHealRun(_ => true);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = heal.RunAsync(cts.Token);
            try
            {
                Assert.True(await heal.Log.WaitForAsync(WaitingForPivot, 1, WaitStartBound),
                    $"the seeded need-heal never waited against a root no peer serves; run={run.Status} {run.Exception?.GetBaseException().Message}; last: {string.Join(" | ", heal.Log.Messages.TakeLast(12))}");
                var requestsWhenWaiting = heal.Scheduler.TrieNodeRequests;
                Assert.True(requestsWhenWaiting > 0, "the seeded heal never asked for the unserved storage trie");

                await Task.Delay(PivotPollInterval + PivotPollInterval);

                Assert.False(run.IsCompleted, "the run ended although nothing could have unblocked the heal");
                Assert.Equal(requestsWhenWaiting, heal.Scheduler.TrieNodeRequests);
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_AHealWaitingOnARootNoPeerServes_When_ASnapCapablePeerJoins_Then_ExactlyOneHealCycleRunsAtTheSameRootAndTheWaitResumes()
        {
            var heal = new NeedHealRun(_ => true);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = heal.RunAsync(cts.Token);
            try
            {
                Assert.True(await heal.Log.WaitForAsync(WaitingForPivot, 1, WaitStartBound), "the heal never started waiting");
                var requestsBeforeJoin = heal.Scheduler.TrieNodeRequests;
                Assert.Equal(1, heal.Log.Count(StalledCycle));

                heal.Pool.Join(new JoiningPeer());

                Assert.True(await heal.Log.WaitForAsync(WaitingForPivot, 2, TimeSpan.FromSeconds(30)),
                    "a joining snap peer did not wake the wait into a heal cycle that waits again");
                var requestsAfterOneCycle = heal.Scheduler.TrieNodeRequests;
                Assert.True(requestsAfterOneCycle > requestsBeforeJoin, "the woken cycle never asked the new peer for the unserved root");
                Assert.Equal(2, heal.Log.Count(StalledCycle));

                await Task.Delay(PivotPollInterval + PivotPollInterval);

                Assert.False(run.IsCompleted);
                Assert.Equal(requestsAfterOneCycle, heal.Scheduler.TrieNodeRequests);
                Assert.Equal(2, heal.Log.Count(StalledCycle));
                Assert.Equal(2, heal.Log.Count(WaitingForPivot));
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_ASnapCapablePeerJoinsWhileAHealCycleIsRunning_When_TheCycleEndsWithoutProgress_Then_TheWaitWakesForThatJoin()
        {
            var heal = new NeedHealRun(_ => true);
            heal.Scheduler.OnFirstTrieNodeRequest = () => heal.Pool.Join(new JoiningPeer());
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = heal.RunAsync(cts.Token);
            try
            {
                Assert.True(await heal.Log.WaitForAsync(WaitingForPivot, 1, WaitStartBound), "the heal never started waiting");
                Assert.True(await heal.Log.WaitForAsync(WaitingForPivot, 2, PivotPollInterval),
                    "a snap peer that joined during the cycle was missed: the wait did not run a cycle for it");
                Assert.Equal(2, heal.Log.Count(StalledCycle));

                await Task.Delay(PivotPollInterval + PivotPollInterval);

                Assert.False(run.IsCompleted);
                Assert.Equal(2, heal.Log.Count(StalledCycle));
                Assert.Equal(2, heal.Log.Count(WaitingForPivot));
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_AHealWaitingOnARootNoPeerServes_When_APeerThatCannotServeSnapStateJoins_Then_NoHealCycleRuns()
        {
            var nonServing = new JoiningPeer();
            var heal = new NeedHealRun(peer => peer.Id != nonServing.Id);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = heal.RunAsync(cts.Token);
            try
            {
                Assert.True(await heal.Log.WaitForAsync(WaitingForPivot, 1, WaitStartBound), "the heal never started waiting");
                var requestsBeforeJoin = heal.Scheduler.TrieNodeRequests;

                heal.Pool.Join(nonServing);
                await Task.Delay(PivotPollInterval + PivotPollInterval);

                Assert.False(run.IsCompleted);
                Assert.Equal(requestsBeforeJoin, heal.Scheduler.TrieNodeRequests);
                Assert.Equal(1, heal.Log.Count(StalledCycle));
                Assert.Equal(1, heal.Log.Count(WaitingForPivot));
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }
    }
}
