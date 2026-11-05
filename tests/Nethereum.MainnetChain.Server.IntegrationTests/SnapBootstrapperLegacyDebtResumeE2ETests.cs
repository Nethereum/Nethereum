using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class SnapBootstrapperLegacyDebtResumeE2ETests : IDisposable
    {
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider Hp = new Sha3KeccackHashProvider();
        private static readonly byte[] AccountHashSeed = { 0x0A, 0xAA };
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_legacydebt_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dbPath)) Directory.Delete(_dbPath, true); } catch { }
        }

        [Fact]
        public async Task Given_A70Snap1StoreWithAnOpenBigAccountChunkedDebtRow_When_TheBootstrapResumes_Then_TheRowIsMigratedHealedAndCleared()
        {
            var (server, stateRoot, accountHash, decoyHash, storageRoot, slots) = BuildWhaleServerState(slotCount: 250);
            var scheduler = new HandlerBackedScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes(), softResponseLimit: 400));
            using var bundle = RocksDbChainStoreBundle.Open(_dbPath, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            Assert.Equal(stateRoot.ToHex(), WriteAccountsTrieLocally(bundle.StateTrieNodes, accountHash, storageRoot, decoyHash).ToHex());
            var pivotHeader = Pivot(stateRoot);
            var pivotHash = Keccak.CalculateHash(new byte[] { 0xFE, 0xED, 0x05 });
            await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 1,
                PivotBlockHash = pivotHash,
                HealTargetRoot = stateRoot,
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });
            var debt = new DeferredStorageDebt
            {
                AccountHash = accountHash,
                DiscoveredStorageRoot = storageRoot,
                FetchStateRoot = stateRoot,
                FetchPivotBlock = 1UL,
                Reason = DeferredStorageReason.BigAccountChunked,
                Status = StorageCompleteness.DeferredBigAccount,
            };
            bundle.Rocks.Put(RocksDbManager.CF_METADATA, RocksDbChainMetadataStore.DeferredStorageDebtKey(accountHash, storageRoot), DeferredStorageDebtCodec.Encode(debt));
            Assert.Equal(1UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            var messages = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var log = new CallbackLogger(messages.Enqueue);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            var result = await SnapBootstrapper.RunAsync(
                bundle, new NeverCalledPeer(), pivotHeader, pivotHash, log,
                new SnapRunOptions
                {
                    Scheduler = scheduler,
                    PivotRefresher = (_, _) => Task.FromResult<(BlockHeader Header, byte[] Hash)?>(null),
                    RunBackfill = false,
                }, cts.Token);

            Assert.True(messages.Any(m => m.Contains("snap.bootstrap.legacy_debts migrated=1", StringComparison.Ordinal)), "the open debt row was not migrated");
            Assert.True(result.Ran);
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
            var healed = PatriciaTrie.LoadFromStorage(storageRoot, bundle.StateTrieNodes, accountHash);
            Assert.Equal(slots.Count, PatriciaRangeIterator.EnumerateRange(healed.Root, bundle.StateTrieNodes, new byte[32]).Count());
            Assert.Equal(slots.Count, FlatSlotCount(bundle, accountHash));
        }

        private static int FlatSlotCount(RocksDbChainStoreBundle bundle, byte[] accountHash)
        {
            var count = 0;
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            for (it.Seek(accountHash); it.Valid() && it.Key().Length == 64 && ByteUtil.StartsWith(it.Key(), accountHash); it.Next()) count++;
            return count;
        }

        private static byte[] WriteAccountsTrieLocally(ITrieNodeStore store, byte[] accountHash, byte[] storageRoot, byte[] decoyAccountHash)
        {
            var trie = new PatriciaTrie(store, Hp);
            trie.Put(accountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1000UL,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            trie.Put(decoyAccountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1UL,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            trie.SaveDirtyNodesToStorageAndCollapse();
            return trie.Root.GetHash();
        }

        private static (InMemoryContentNodeStore Server, byte[] StateRoot, byte[] AccountHash, byte[] DecoyAccountHash,
            byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Slots) BuildWhaleServerState(int slotCount)
        {
            var server = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(server, Hp);
            var slots = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < slotCount; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { (byte)(i >> 8), (byte)(i & 0xFF), 0x77 });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xEE, (byte)(i & 0xFF), (byte)((i >> 4) & 0xFF) });
                slots.Add((slotHash, value));
                storageTrie.Put(slotHash, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();
            var accountHash = Keccak.CalculateHash(AccountHashSeed);
            byte seed = 0;
            byte[] decoy;
            do { decoy = Keccak.CalculateHash(new byte[] { 0x0D, 0xEC, seed++ }); }
            while (decoy[0] >> 4 == accountHash[0] >> 4);
            var stateRoot = WriteAccountsTrieLocally(server, accountHash, storageRoot, decoy);
            return (server, stateRoot, accountHash, decoy, storageRoot, slots);
        }

        private static BlockHeader Pivot(byte[] stateRoot) => new BlockHeader
        {
            BlockNumber = 1,
            StateRoot = stateRoot,
            ParentHash = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            UnclesHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
            Difficulty = 0,
            GasLimit = 0,
            GasUsed = 0,
            Timestamp = 0,
            MixHash = new byte[32],
            Nonce = new byte[8],
        };

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class NeverCalledPeer : ISnapPeer
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException("heal-only resume must not touch the snap peer");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage req, CancellationToken ct = default)
                => throw new InvalidOperationException();
        }

        private sealed class CallbackLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly Action<string> _onMessage;
            public CallbackLogger(Action<string> onMessage) { _onMessage = onMessage; }
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _onMessage(formatter(state, exception));
        }

        private sealed class HandlerBackedScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;

            public HandlerBackedScheduler(PatriciaSnapRequestHandler handler) => _handler = handler;

            public Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1, RootHash = stateRoot, StartingHash = startingHash, LimitHash = limitHash, ResponseBytes = responseBytes,
                }, ct);

            public Task<StorageRangesMessage> FetchStorageRangesAsync(
                byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => _handler.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = 1, RootHash = stateRoot, AccountHashes = accountHashes, StartingHash = startingHash, LimitHash = limitHash, ResponseBytes = responseBytes,
                }, ct);

            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 1, RootHash = stateRoot, Paths = paths, ResponseBytes = responseBytes,
                }, ct);

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}
