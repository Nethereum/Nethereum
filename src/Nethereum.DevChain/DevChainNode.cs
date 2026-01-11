using System;
using System.Collections.Generic;
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

namespace Nethereum.DevChain
{
    public class DevChainNode : IDevChainNode
    {
        private readonly DevChainConfig _config;
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IStateStore _stateStore;
        private readonly IFilterStore _filterStore;
        private readonly INodeDataService _nodeDataService;
        private readonly TransactionProcessor _transactionProcessor;
        private readonly BlockManager _blockManager;
        private readonly ITransactionVerificationAndRecovery _txVerifier;
        private readonly Dictionary<int, CoreChain.Storage.IStateSnapshot> _snapshots = new Dictionary<int, CoreChain.Storage.IStateSnapshot>();

        private bool _initialized;

        public DevChainNode() : this(DevChainConfig.Default)
        {
        }

        public DevChainNode(DevChainConfig config)
        {
            _config = config ?? DevChainConfig.Default;

            _blockStore = new InMemoryBlockStore();
            _transactionStore = new InMemoryTransactionStore();
            _receiptStore = new InMemoryReceiptStore();
            _logStore = new InMemoryLogStore();
            _stateStore = new InMemoryStateStore();
            _filterStore = new InMemoryFilterStore();
            _txVerifier = new TransactionVerificationAndRecoveryImp();

            if (_config.IsForkEnabled)
            {
                var rpcClient = new RpcClient(new Uri(_config.ForkUrl));
                var ethApiService = new EthApiService(rpcClient);
                var forkBlock = _config.ForkBlockNumber.HasValue
                    ? new BlockParameter((ulong)_config.ForkBlockNumber.Value)
                    : BlockParameter.CreateLatest();

                _nodeDataService = new ForkingNodeDataService(
                    _stateStore, _blockStore, ethApiService, forkBlock);
            }
            else
            {
                _nodeDataService = new StateStoreNodeDataService(_stateStore, _blockStore);
            }

            _transactionProcessor = new TransactionProcessor(
                _stateStore,
                _blockStore,
                _config,
                _txVerifier);

            _blockManager = new BlockManager(
                _blockStore,
                _transactionStore,
                _receiptStore,
                _logStore,
                _stateStore,
                _transactionProcessor,
                _config);
        }

        public DevChainConfig DevConfig => _config;
        ChainConfig IChainNode.Config => _config;
        public IBlockStore Blocks => _blockStore;
        public ITransactionStore Transactions => _transactionStore;
        public IReceiptStore Receipts => _receiptStore;
        public ILogStore Logs => _logStore;
        public IStateStore State => _stateStore;
        public IFilterStore Filters => _filterStore;
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

        public async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            EnsureInitialized();
            return await _blockManager.SendTransactionAsync(tx);
        }

        public async Task<byte[]> MineBlockAsync()
        {
            EnsureInitialized();
            return await _blockManager.MineBlockAsync();
        }

        public async Task<byte[]> MineBlockWithTransactionAsync(ISignedTransaction tx)
        {
            EnsureInitialized();
            return await _blockManager.MineBlockWithTransactionAsync(tx);
        }

        public async Task<BlockHeader> GetBlockByHashAsync(byte[] blockHash)
        {
            return await _blockStore.GetByHashAsync(blockHash);
        }

        public async Task<BlockHeader> GetBlockByNumberAsync(BigInteger blockNumber)
        {
            return await _blockStore.GetByNumberAsync(blockNumber);
        }

        public async Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber)
        {
            return await _blockStore.GetHashByNumberAsync(blockNumber);
        }

        public async Task<BlockHeader> GetLatestBlockAsync()
        {
            return await _blockStore.GetLatestAsync();
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            return await _blockStore.GetHeightAsync();
        }

        public async Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash)
        {
            return await _transactionStore.GetByHashAsync(txHash);
        }

        public async Task<Receipt> GetTransactionReceiptAsync(byte[] txHash)
        {
            return await _receiptStore.GetByTxHashAsync(txHash);
        }

        public async Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash)
        {
            return await _receiptStore.GetInfoByTxHashAsync(txHash);
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await _nodeDataService.GetBalanceAsync(address);
        }

        public async Task<BigInteger> GetNonceAsync(string address)
        {
            return await _nodeDataService.GetTransactionCount(address);
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            return await _nodeDataService.GetCodeAsync(address);
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger slot)
        {
            return await _nodeDataService.GetStorageAtAsync(address, slot);
        }

        public async Task SetBalanceAsync(string address, BigInteger balance)
        {
            var account = await _stateStore.GetAccountAsync(address)
                ?? new Account { Nonce = 0 };
            account.Balance = balance;
            await _stateStore.SaveAccountAsync(address, account);
        }

        public async Task SetNonceAsync(string address, BigInteger nonce)
        {
            var account = await _stateStore.GetAccountAsync(address)
                ?? new Account { Balance = 0 };
            account.Nonce = nonce;
            await _stateStore.SaveAccountAsync(address, account);
        }

        public async Task SetCodeAsync(string address, byte[] code)
        {
            var codeHash = new Nethereum.Util.Sha3Keccack().CalculateHash(code);
            await _stateStore.SaveCodeAsync(codeHash, code);

            var account = await _stateStore.GetAccountAsync(address)
                ?? new Account { Balance = 0, Nonce = 1 };
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

        public BlockContext GetPendingBlockContext()
        {
            return _blockManager.GetPendingBlockContext();
        }

        public int GetPendingTransactionCount()
        {
            return _blockManager.GetPendingTransactionCount();
        }

        public List<ISignedTransaction> GetPendingTransactions()
        {
            return _blockManager.GetPendingTransactions();
        }

        public async Task<CallResult> CallAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            EnsureInitialized();

            from = from ?? "0x0000000000000000000000000000000000000000";
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = gasLimit ?? 10_000_000;

            var blockContext = _blockManager.GetPendingBlockContext();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var code = await _nodeDataService.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                return new CallResult { Success = true, ReturnData = new byte[0], GasUsed = 0 };
            }

            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var callInput = new CallInput
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
            await simulator.ExecuteAsync(program, traceEnabled: false);

            return new CallResult
            {
                Success = !program.ProgramResult.IsRevert,
                ReturnData = program.ProgramResult.Result ?? new byte[0],
                RevertReason = program.ProgramResult.GetRevertMessage(),
                GasUsed = program.TotalGasUsed
            };
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
            var isContractCreation = string.IsNullOrEmpty(GetTransactionTo(tx));
            var to = isContractCreation ? receiptInfo.ContractAddress : GetTransactionTo(tx);
            var data = GetTransactionData(tx);
            var value = GetTransactionValue(tx);
            var gasLimit = GetTransactionGasLimit(tx);

            var blockContext = new BlockContext
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
            await simulator.ExecuteAsync(program, traceEnabled: true);

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
            await simulator.ExecuteAsync(program, traceEnabled: true);

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
                        accountState.UpsertStorageValue(slot, value);
                    }
                }

                if (overrideDto.StateDiff != null)
                {
                    foreach (var storageKvp in overrideDto.StateDiff)
                    {
                        var slot = storageKvp.Key.HexToBigInteger(false);
                        var value = storageKvp.Value.HexToByteArray();
                        accountState.UpsertStorageValue(slot, value);
                    }
                }
            }
        }

        private string GetTransactionTo(ISignedTransaction tx)
        {
            switch (tx)
            {
                case Transaction1559 tx1559:
                    return tx1559.ReceiverAddress;
                case Transaction2930 tx2930:
                    return tx2930.ReceiverAddress;
                case LegacyTransaction legacyTx:
                    return legacyTx.ReceiveAddress?.ToHex(true);
                case LegacyTransactionChainId legacyChainTx:
                    return legacyChainTx.ReceiveAddress?.ToHex(true);
                default:
                    return null;
            }
        }

        private byte[] GetTransactionData(ISignedTransaction tx)
        {
            switch (tx)
            {
                case Transaction1559 tx1559:
                    return tx1559.Data?.HexToByteArray();
                case Transaction2930 tx2930:
                    return tx2930.Data?.HexToByteArray();
                case LegacyTransaction legacyTx:
                    return legacyTx.Data;
                case LegacyTransactionChainId legacyChainTx:
                    return legacyChainTx.Data;
                default:
                    return null;
            }
        }

        private BigInteger GetTransactionValue(ISignedTransaction tx)
        {
            switch (tx)
            {
                case Transaction1559 tx1559:
                    return tx1559.Amount ?? 0;
                case Transaction2930 tx2930:
                    return tx2930.Amount ?? 0;
                case LegacyTransaction legacyTx:
                    return legacyTx.Value.ToBigIntegerFromRLPDecoded();
                case LegacyTransactionChainId legacyChainTx:
                    return legacyChainTx.Value.ToBigIntegerFromRLPDecoded();
                default:
                    return 0;
            }
        }

        private BigInteger GetTransactionGasLimit(ISignedTransaction tx)
        {
            switch (tx)
            {
                case Transaction1559 tx1559:
                    return tx1559.GasLimit ?? 21000;
                case Transaction2930 tx2930:
                    return tx2930.GasLimit ?? 21000;
                case LegacyTransaction legacyTx:
                    return legacyTx.GasLimit.ToBigIntegerFromRLPDecoded();
                case LegacyTransactionChainId legacyChainTx:
                    return legacyChainTx.GasLimit.ToBigIntegerFromRLPDecoded();
                default:
                    return 21000;
            }
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("DevChainNode must be initialized by calling StartAsync() before use.");
        }
    }
}
