using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.RLP;
using Nethereum.RPC;
using Nethereum.JsonRpc.Client;
using Nethereum.Util;
using Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.DevChain
{
    public class DevChainNode : ChainNodeBase, IDevChainNode, IDisposable
    {
        private readonly DevChainConfig _config;
        private readonly BlockManager _blockManager;
        private readonly object _snapshotsLock = new object();
        private readonly Dictionary<int, SnapshotInfo> _snapshots = new();
        private volatile bool _initialized;
        private bool _disposed;

        private SqliteStorageManager _sqliteManager;

        public DevChainNode() : this(DevChainConfig.Default)
        {
        }

        public DevChainNode(DevChainConfig config) : this(config, CreateSqliteManager(null, true))
        {
        }

        private DevChainNode(DevChainConfig config, SqliteStorageManager sqliteManager) : this(
            config,
            new SqliteBlockStore(sqliteManager),
            new SqliteTransactionStore(sqliteManager),
            new SqliteReceiptStore(sqliteManager),
            new SqliteLogStore(sqliteManager),
            new HistoricalStateStore(new SqliteStateStore(sqliteManager), new SqliteStateDiffStore(sqliteManager), HistoricalStateOptions.DevChainDefault),
            new InMemoryFilterStore(),
            new SqliteTrieNodeStore(sqliteManager))
        {
            _sqliteManager = sqliteManager;
        }

        public DevChainNode(DevChainConfig config, string dbPath, bool persistDb = false)
            : this(config, CreateSqliteManager(dbPath, !persistDb))
        {
        }

        private static SqliteStorageManager CreateSqliteManager(string dbPath, bool deleteOnDispose)
        {
            return new SqliteStorageManager(dbPath, deleteOnDispose);
        }

        public static DevChainNode CreateInMemory(DevChainConfig config = null)
        {
            config = config ?? DevChainConfig.Default;
            var blockStore = new InMemoryBlockStore();
            return new DevChainNode(
                config,
                blockStore,
                new InMemoryTransactionStore(blockStore),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                new HistoricalStateStore(new InMemoryStateStore(), new InMemoryStateDiffStore(), HistoricalStateOptions.DevChainDefault),
                new InMemoryFilterStore(),
                new InMemoryTrieNodeStore());
        }

        public DevChainNode(
            DevChainConfig config,
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            IFilterStore filterStore,
            ITrieNodeStore trieNodeStore = null)
            : base(
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                filterStore,
                CreateTransactionProcessor(stateStore, blockStore, config, SharedTxVerifier),
                SharedTxVerifier,
                CreateNodeDataService(stateStore, blockStore, config),
                trieNodeStore)
        {
            _config = config ?? DevChainConfig.Default;

            _blockManager = new BlockManager(
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                _transactionProcessor,
                _txVerifier,
                _config,
                trieNodeStore);
        }

        private static readonly ITransactionVerificationAndRecovery SharedTxVerifier = new TransactionVerificationAndRecoveryImp();

        private static TransactionProcessor CreateTransactionProcessor(
            IStateStore stateStore, IBlockStore blockStore, DevChainConfig config, ITransactionVerificationAndRecovery txVerifier)
        {
            var effectiveConfig = config ?? DevChainConfig.Default;
            return new TransactionProcessor(
                stateStore,
                blockStore,
                effectiveConfig,
                txVerifier,
                effectiveConfig.GetHardforkConfig());
        }

        private static INodeDataService CreateNodeDataService(
            IStateStore stateStore, IBlockStore blockStore, DevChainConfig config)
        {
            if (config?.IsForkEnabled == true)
            {
                var rpcClient = new RpcClient(new Uri(config.ForkUrl));
                var ethApiService = new EthApiService(rpcClient);
                var forkBlock = config.ForkBlockNumber.HasValue
                    ? new BlockParameter((ulong)config.ForkBlockNumber.Value)
                    : BlockParameter.CreateLatest();

                return new ForkingNodeDataService(stateStore, blockStore, ethApiService, forkBlock);
            }
            return new StateStoreNodeDataService(stateStore, blockStore);
        }

        public override ChainConfig Config => _config;
        public DevChainConfig DevConfig => _config;
        public BlockManager BlockManager => _blockManager;

        public async Task StartAsync()
        {
            if (_initialized)
                return;

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(IEnumerable<string> prefundedAddresses)
        {
            if (_initialized)
                return;

            foreach (var address in prefundedAddresses)
            {
                await SetBalanceAsync(address, _config.InitialBalance);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(IEnumerable<string> prefundedAddresses, BigInteger balance)
        {
            if (_initialized)
                return;

            foreach (var address in prefundedAddresses)
            {
                await SetBalanceAsync(address, balance);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(params Account[] accounts)
        {
            if (_initialized)
                return;

            foreach (var account in accounts)
            {
                await SetBalanceAsync(account.Address, _config.InitialBalance);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(IEnumerable<Account> accounts, BigInteger balance)
        {
            if (_initialized)
                return;

            foreach (var account in accounts)
            {
                await SetBalanceAsync(account.Address, balance);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(params EthECKey[] keys)
        {
            if (_initialized)
                return;

            foreach (var key in keys)
            {
                await SetBalanceAsync(key.GetPublicAddress(), _config.InitialBalance);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(IEnumerable<EthECKey> keys, BigInteger balance)
        {
            if (_initialized)
                return;

            foreach (var key in keys)
            {
                await SetBalanceAsync(key.GetPublicAddress(), balance);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public async Task StartAsync(IDictionary<string, BigInteger> addressBalances)
        {
            if (_initialized)
                return;

            foreach (var kvp in addressBalances)
            {
                await SetBalanceAsync(kvp.Key, kvp.Value);
            }

            await _blockManager.InitializeAsync();
            _initialized = true;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(DevChainConfig config = null)
        {
            var node = new DevChainNode(config ?? DevChainConfig.Default);
            await node.StartAsync();
            return node;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(params string[] prefundedAddresses)
        {
            var node = new DevChainNode();
            await node.StartAsync(prefundedAddresses);
            return node;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(params Account[] accounts)
        {
            var node = new DevChainNode();
            await node.StartAsync(accounts);
            return node;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(params EthECKey[] keys)
        {
            var node = new DevChainNode();
            await node.StartAsync(keys);
            return node;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(DevChainConfig config, params Account[] accounts)
        {
            var node = new DevChainNode(config);
            await node.StartAsync(accounts);
            return node;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(DevChainConfig config, params EthECKey[] keys)
        {
            var node = new DevChainNode(config);
            await node.StartAsync(keys);
            return node;
        }

        public static async Task<DevChainNode> CreateAndStartAsync(IDictionary<string, BigInteger> addressBalances, DevChainConfig config = null)
        {
            var node = new DevChainNode(config ?? DevChainConfig.Default);
            await node.StartAsync(addressBalances);
            return node;
        }

        public Account[] GenerateAndFundAccounts(int count)
        {
            var accounts = new Account[count];
            var chainId = (int)_config.ChainId;
            for (int i = 0; i < count; i++)
            {
                var key = EthECKey.GenerateKey();
                accounts[i] = new Account(key.GetPrivateKey(), chainId);
            }
            return accounts;
        }

        public async Task<Account[]> GenerateAndFundAccountsAsync(int count, BigInteger? balance = null)
        {
            var accounts = GenerateAndFundAccounts(count);
            var fundBalance = balance ?? _config.InitialBalance;

            foreach (var account in accounts)
            {
                await SetBalanceAsync(account.Address, fundBalance);
            }

            return accounts;
        }

        public override async Task<CoreChain.TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            EnsureInitialized();
            return await _blockManager.SendTransactionAsync(tx);
        }

        public async Task<byte[]> MineBlockAsync()
        {
            EnsureInitialized();
            return await _blockManager.MineBlockAsync();
        }

        public async Task<byte[]> MineBlockAsync(byte[] parentBeaconBlockRoot)
        {
            EnsureInitialized();
            return await _blockManager.MineBlockAsync(parentBeaconBlockRoot);
        }

        public async Task<byte[]> MineBlockWithTransactionAsync(ISignedTransaction tx)
        {
            EnsureInitialized();
            return await _blockManager.MineBlockWithTransactionAsync(tx);
        }

        public async Task SetBalanceAsync(string address, BigInteger balance)
        {
            var account = await _stateStore.GetAccountAsync(address)
                ?? new Model.Account { Nonce = 0 };
            account.Balance = balance;
            await _stateStore.SaveAccountAsync(address, account);
        }

        public async Task SetBlockHashAsync(BigInteger blockNumber, byte[] hash)
        {
            await _blockStore.UpdateBlockHashAsync(blockNumber, hash);
        }

        public async Task SetNonceAsync(string address, BigInteger nonce)
        {
            var account = await _stateStore.GetAccountAsync(address)
                ?? new Model.Account { Balance = 0 };
            account.Nonce = nonce;
            await _stateStore.SaveAccountAsync(address, account);
        }

        private static readonly Sha3Keccack _keccak = new();

        public async Task SetCodeAsync(string address, byte[] code)
        {
            var codeHash = _keccak.CalculateHash(code);
            await _stateStore.SaveCodeAsync(codeHash, code);

            var account = await _stateStore.GetAccountAsync(address)
                ?? new Model.Account { Balance = 0, Nonce = 1 };
            account.CodeHash = codeHash;
            await _stateStore.SaveAccountAsync(address, account);
        }

        public async Task SetStorageAtAsync(string address, BigInteger slot, byte[] value)
        {
            await _stateStore.SaveStorageAsync(address, slot, value);
        }

        public async Task<CoreChain.Storage.IStateSnapshot> TakeSnapshotAsync()
        {
            var snapshot = await _stateStore.CreateSnapshotAsync();
            var latestBlock = await _blockStore.GetLatestAsync();
            var blockHeight = latestBlock?.BlockNumber ?? 0;

            lock (_snapshotsLock)
            {
                _snapshots[snapshot.SnapshotId] = new SnapshotInfo(snapshot, blockHeight);
            }
            return snapshot;
        }

        public async Task RevertToSnapshotAsync(CoreChain.Storage.IStateSnapshot snapshot)
        {
            SnapshotInfo info;
            lock (_snapshotsLock)
            {
                if (!_snapshots.TryGetValue(snapshot.SnapshotId, out info))
                    info = new SnapshotInfo(snapshot, 0);

                var staleIds = new List<int>();
                foreach (var kvp in _snapshots)
                {
                    if (kvp.Key > snapshot.SnapshotId)
                    {
                        kvp.Value.StateSnapshot.Dispose();
                        staleIds.Add(kvp.Key);
                    }
                }
                foreach (var id in staleIds)
                {
                    _snapshots.Remove(id);
                }
            }

            await _stateStore.RevertSnapshotAsync(info.StateSnapshot);

            if (info.BlockHeight > 0)
            {
                await PruneStoresAfterBlockAsync(info.BlockHeight);

                if (_stateStore is HistoricalStateStore historicalStore)
                    await historicalStore.PurgeDiffsAboveBlockAsync(info.BlockHeight);
            }

            await _blockManager.ReinitializePendingBlockAsync();
        }

        private async Task PruneStoresAfterBlockAsync(System.Numerics.BigInteger snapshotBlockHeight)
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            if (latestBlock == null) return;

            for (var blockNum = latestBlock.BlockNumber; blockNum > snapshotBlockHeight; blockNum--)
            {
                await _logStore.DeleteByBlockNumberAsync(blockNum);
                await _receiptStore.DeleteByBlockNumberAsync(blockNum);
                await _transactionStore.DeleteByBlockNumberAsync(blockNum);
                await _blockStore.DeleteByNumberAsync(blockNum);
            }
        }

        public CoreChain.BlockContext GetPendingBlockContext()
        {
            return _blockManager.GetPendingBlockContext();
        }

        public int GetPendingTransactionCount()
        {
            return _blockManager.GetPendingTransactionCount();
        }

        public override Task<List<ISignedTransaction>> GetPendingTransactionsAsync()
        {
            return Task.FromResult(_blockManager.GetPendingTransactions());
        }

        protected override Task<BlockContext> GetBlockContextForCallAsync()
        {
            EnsureInitialized();
            return Task.FromResult(_blockManager.GetPendingBlockContext());
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("DevChainNode must be initialized by calling StartAsync() before use.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                lock (_snapshotsLock)
                {
                    foreach (var info in _snapshots.Values)
                    {
                        info.StateSnapshot.Dispose();
                    }
                    _snapshots.Clear();
                }

                _blockManager?.Dispose();

                if (_stateStore is InMemoryStateStore memStateStore)
                    memStateStore.Clear();
                if (_blockStore is InMemoryBlockStore memBlockStore)
                    memBlockStore.Clear();
                if (_transactionStore is InMemoryTransactionStore memTxStore)
                    memTxStore.Clear();
                if (_receiptStore is InMemoryReceiptStore memReceiptStore)
                    memReceiptStore.Clear();
                if (_logStore is InMemoryLogStore memLogStore)
                    memLogStore.Clear();

                _sqliteManager?.Dispose();
                _sqliteManager = null;

                _initialized = false;
            }
        }
    }

    internal class SnapshotInfo
    {
        public CoreChain.Storage.IStateSnapshot StateSnapshot { get; }
        public System.Numerics.BigInteger BlockHeight { get; }

        public SnapshotInfo(CoreChain.Storage.IStateSnapshot stateSnapshot, System.Numerics.BigInteger blockHeight)
        {
            StateSnapshot = stateSnapshot;
            BlockHeight = blockHeight;
        }
    }
}
