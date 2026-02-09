using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.Debug.Tracers;
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
        private readonly Dictionary<int, CoreChain.Storage.IStateSnapshot> _snapshots = new Dictionary<int, CoreChain.Storage.IStateSnapshot>();

        private bool _initialized;

        public DevChainNode() : this(DevChainConfig.Default)
        {
        }

        public DevChainNode(DevChainConfig config) : this(
            config,
            new InMemoryBlockStore(),
            new InMemoryTransactionStore(),
            new InMemoryReceiptStore(),
            new InMemoryLogStore(),
            new InMemoryStateStore(),
            new InMemoryFilterStore(),
            new InMemoryTrieNodeStore())
        {
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
                CreateTransactionProcessor(stateStore, blockStore, config),
                new TransactionVerificationAndRecoveryImp(),
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

        private static TransactionProcessor CreateTransactionProcessor(
            IStateStore stateStore, IBlockStore blockStore, DevChainConfig config)
        {
            var effectiveConfig = config ?? DevChainConfig.Default;
            return new TransactionProcessor(
                stateStore,
                blockStore,
                effectiveConfig,
                new TransactionVerificationAndRecoveryImp(),
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

        public async Task SetCodeAsync(string address, byte[] code)
        {
            var codeHash = new Sha3Keccack().CalculateHash(code);
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
            _snapshots[snapshot.SnapshotId] = snapshot;
            return snapshot;
        }

        public async Task RevertToSnapshotAsync(CoreChain.Storage.IStateSnapshot snapshot)
        {
            if (_snapshots.TryGetValue(snapshot.SnapshotId, out var storedSnapshot))
            {
                await _stateStore.RevertSnapshotAsync(storedSnapshot);
            }
            else
            {
                await _stateStore.RevertSnapshotAsync(snapshot);
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

        public async Task<OpcodeTracerResponse> TraceTransactionAsync(
            string txHash,
            OpcodeTracerConfigDto config = null)
        {
            EnsureInitialized();

            var txHashBytes = txHash.HexToByteArray();
            var tx = await _transactionStore.GetByHashAsync(txHashBytes);
            if (tx == null)
                throw new InvalidOperationException($"Transaction {txHash} not found");

            var receiptInfo = await _receiptStore.GetInfoByTxHashAsync(txHashBytes);
            if (receiptInfo == null)
                throw new InvalidOperationException($"Receipt for transaction {txHash} not found");

            var block = await _blockStore.GetByHashAsync(receiptInfo.BlockHash);
            if (block == null)
                throw new InvalidOperationException($"Block for transaction {txHash} not found");

            var from = _txVerifier.GetSenderAddress(tx);
            var isContractCreation = tx.IsContractCreation();
            var to = isContractCreation ? receiptInfo.ContractAddress : tx.GetReceiverAddress();
            var data = tx.GetData();
            var value = tx.GetValue();
            var gasLimit = tx.GetGasLimit();

            var blockContext = new CoreChain.BlockContext
            {
                BlockNumber = block.BlockNumber,
                Timestamp = block.Timestamp,
                Coinbase = block.Coinbase ?? _config.Coinbase,
                Difficulty = block.Difficulty,
                GasLimit = block.GasLimit,
                BaseFee = block.BaseFee ?? 0,
                ChainId = _config.ChainId
            };

            var code = await _nodeDataService.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                return new OpcodeTracerResponse
                {
                    Gas = 0,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<StructLog>()
                };
            }

            var executionStateService = new ExecutionStateService(_nodeDataService);
            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var callInput = new CallInput
            {
                From = from,
                To = to,
                Value = new Hex.HexTypes.HexBigInteger(value),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new Hex.HexTypes.HexBigInteger(gasLimit),
                GasPrice = new Hex.HexTypes.HexBigInteger(0),
                ChainId = new Hex.HexTypes.HexBigInteger(_config.ChainId)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                from,
                to,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(code, programContext);
            var simulator = new EVMSimulator();
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            return TraceConverter.ConvertToOpcodeResponse(program, config);
        }

        public async Task<OpcodeTracerResponse> TraceCallAsync(
            CallInput callInput,
            OpcodeTracerConfigDto config = null,
            Dictionary<string, StateOverrideDto> stateOverrides = null)
        {
            EnsureInitialized();

            var from = callInput.From ?? "0x0000000000000000000000000000000000000000";
            var to = callInput.To;
            var callValue = callInput.Value?.Value ?? BigInteger.Zero;
            var callGasLimit = callInput.Gas?.Value ?? 10_000_000;
            var data = callInput.Data?.HexToByteArray();

            var blockContext = _blockManager.GetPendingBlockContext();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            if (stateOverrides != null)
            {
                await ApplyStateOverridesAsync(executionStateService, stateOverrides);
            }

            var code = await _nodeDataService.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                return new OpcodeTracerResponse
                {
                    Gas = 0,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<StructLog>()
                };
            }

            var traceCallInput = new CallInput
            {
                From = from,
                To = to,
                Value = new Hex.HexTypes.HexBigInteger(callValue),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new Hex.HexTypes.HexBigInteger(callGasLimit),
                GasPrice = new Hex.HexTypes.HexBigInteger(0),
                ChainId = new Hex.HexTypes.HexBigInteger(_config.ChainId)
            };

            var programContext = new ProgramContext(
                traceCallInput,
                executionStateService,
                from,
                to,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(code, programContext);
            var simulator = new EVMSimulator();
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            return TraceConverter.ConvertToOpcodeResponse(program, config);
        }

        private async Task ApplyStateOverridesAsync(
            ExecutionStateService executionStateService,
            Dictionary<string, StateOverrideDto> overrides)
        {
            foreach (var kvp in overrides)
            {
                var address = kvp.Key;
                var overrideDto = kvp.Value;
                var accountState = executionStateService.CreateOrGetAccountExecutionState(address);

                if (overrideDto.Balance != null)
                {
                    executionStateService.SetInitialChainBalance(address, overrideDto.Balance.Value);
                }

                if (!string.IsNullOrEmpty(overrideDto.Code))
                {
                    accountState.Code = overrideDto.Code.HexToByteArray();
                }

                if (overrideDto.State != null)
                {
                    foreach (var storageKvp in overrideDto.State)
                    {
                        var slot = storageKvp.Key.HexToBigInteger(false);
                        var value = storageKvp.Value.HexToByteArray();
                        accountState.SetPreStateStorage(slot, value);
                    }
                }

                if (overrideDto.StateDiff != null)
                {
                    foreach (var storageKvp in overrideDto.StateDiff)
                    {
                        var slot = storageKvp.Key.HexToBigInteger(false);
                        var value = storageKvp.Value.HexToByteArray();
                        accountState.SetPreStateStorage(slot, value);
                    }
                }
            }
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
            if (disposing)
            {
                foreach (var snapshot in _snapshots.Values)
                {
                    snapshot.Dispose();
                }
                _snapshots.Clear();

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

                _initialized = false;
            }
        }
    }
}
