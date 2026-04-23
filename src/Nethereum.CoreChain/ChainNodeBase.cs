using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract class ChainNodeBase : IChainNode
    {
        protected readonly IBlockStore _blockStore;
        protected readonly ITransactionStore _transactionStore;
        protected readonly IReceiptStore _receiptStore;
        protected readonly ILogStore _logStore;
        protected readonly IStateStore _stateStore;
        protected readonly IFilterStore _filterStore;
        protected readonly ITrieNodeStore _trieNodeStore;
        protected readonly IBlobStore _blobStore;
        protected readonly IStateReader _nodeDataService;
        protected readonly TransactionProcessor _transactionProcessor;
        protected readonly ITransactionVerificationAndRecovery _txVerifier;
        protected readonly TransactionExecutor _executor;

        protected ChainNodeBase(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            IFilterStore filterStore,
            TransactionProcessor transactionProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            IStateReader nodeDataService = null,
            ITrieNodeStore trieNodeStore = null,
            IBlobStore blobStore = null,
            HardforkConfig hardforkConfig = null)
        {
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _filterStore = filterStore ?? throw new ArgumentNullException(nameof(filterStore));
            _transactionProcessor = transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor));
            _txVerifier = txVerifier ?? throw new ArgumentNullException(nameof(txVerifier));
            _nodeDataService = nodeDataService ?? new StateStoreNodeDataService(_stateStore, _blockStore);
            _trieNodeStore = trieNodeStore;
            _blobStore = blobStore;
            _executor = new TransactionExecutor(hardforkConfig ?? throw new ArgumentNullException(nameof(hardforkConfig)));
        }

        public abstract ChainConfig Config { get; }
        public IBlockStore Blocks => _blockStore;
        public ITransactionStore Transactions => _transactionStore;
        public IReceiptStore Receipts => _receiptStore;
        public ILogStore Logs => _logStore;
        public IStateStore State => _stateStore;
        public IFilterStore Filters => _filterStore;
        public ITrieNodeStore TrieNodes => _trieNodeStore;
        public IBlobStore BlobStore => _blobStore;

        private Services.IProofService _proofService;
        public virtual Services.IProofService ProofService =>
            _proofService ??= new Services.ProofService(_stateStore, _trieNodeStore);

        public virtual async Task<BigInteger> GetBlockNumberAsync()
        {
            return await _blockStore.GetHeightAsync();
        }

        public virtual async Task<BlockHeader> GetBlockByHashAsync(byte[] hash)
        {
            return await _blockStore.GetByHashAsync(hash);
        }

        public virtual async Task<BlockHeader> GetBlockByNumberAsync(BigInteger number)
        {
            return await _blockStore.GetByNumberAsync(number);
        }

        public virtual async Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber)
        {
            return await _blockStore.GetHashByNumberAsync(blockNumber);
        }

        public virtual async Task<BlockHeader> GetLatestBlockAsync()
        {
            return await _blockStore.GetLatestAsync();
        }

        public virtual async Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash)
        {
            return await _transactionStore.GetByHashAsync(txHash);
        }

        public virtual async Task<Receipt> GetTransactionReceiptAsync(byte[] txHash)
        {
            return await _receiptStore.GetByTxHashAsync(txHash);
        }

        public virtual async Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash)
        {
            return await _receiptStore.GetInfoByTxHashAsync(txHash);
        }

        public virtual async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await _nodeDataService.GetBalanceAsync(address);
        }

        public virtual async Task<BigInteger> GetNonceAsync(string address)
        {
            return await _nodeDataService.GetTransactionCountAsync(address);
        }

        public virtual async Task<byte[]> GetCodeAsync(string address)
        {
            return await _nodeDataService.GetCodeAsync(address);
        }

        public virtual async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot)
        {
            return await _nodeDataService.GetStorageAtAsync(address, slot);
        }

        public virtual async Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber)
        {
            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            return await dataService.GetBalanceAsync(address);
        }

        public virtual async Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber)
        {
            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            return await dataService.GetTransactionCountAsync(address);
        }

        public virtual async Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber)
        {
            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            return await dataService.GetCodeAsync(address);
        }

        public virtual async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot, BigInteger blockNumber)
        {
            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            return await dataService.GetStorageAtAsync(address, slot);
        }

        public virtual async Task<CallResult> CallAsync(string to, byte[] data, string from = null,
            BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = gasLimit ?? 10_000_000;

            var blockContext = await GetBlockContextForCallAsync();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var isContractCreation = string.IsNullOrEmpty(to);

            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = from,
                To = isContractCreation ? null : to,
                Data = data,
                Value = callValue,
                GasLimit = callGasLimit,
                GasPrice = 0,
                MaxFeePerGas = 0,
                MaxPriorityFeePerGas = 0,
                Nonce = 0,
                IsEip1559 = false,
                IsContractCreation = isContractCreation,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                ExecutionState = executionStateService,
                TraceEnabled = false
            };

            var result = await _executor.ExecuteAsync(ctx);

            return new CallResult
            {
                Success = result.Success,
                ReturnData = result.ReturnData ?? Array.Empty<byte>(),
                RevertReason = result.RevertReason ?? result.Error,
                GasUsed = result.GasUsed
            };
        }

        public virtual async Task<CallResult> CallAsync(string to, byte[] data, BigInteger blockNumber,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = gasLimit ?? 10_000_000;

            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            var executionStateService = new ExecutionStateService(dataService);

            var isContractCreation = string.IsNullOrEmpty(to);

            var callerBalance = await dataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = from,
                To = isContractCreation ? null : to,
                Data = data,
                Value = callValue,
                GasLimit = callGasLimit,
                GasPrice = 0,
                MaxFeePerGas = 0,
                MaxPriorityFeePerGas = 0,
                Nonce = 0,
                IsEip1559 = false,
                IsContractCreation = isContractCreation,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                ExecutionState = executionStateService,
                TraceEnabled = false
            };

            var result = await _executor.ExecuteAsync(ctx);

            return new CallResult
            {
                Success = result.Success,
                ReturnData = result.ReturnData ?? Array.Empty<byte>(),
                RevertReason = result.RevertReason ?? result.Error,
                GasUsed = result.GasUsed
            };
        }

        public virtual async Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var createValue = value ?? BigInteger.Zero;
            var createGasLimit = gasLimit ?? 10_000_000;

            if (initCode == null || initCode.Length == 0)
            {
                return new CallResult { Success = true, ReturnData = Array.Empty<byte>(), GasUsed = 0 };
            }

            var blockContext = await GetBlockContextForCallAsync();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var contractAddress = ContractUtils.CalculateContractAddress(from, 0);

            var callInput = new CallInput
            {
                From = from,
                To = contractAddress,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(createValue),
                Data = initCode.ToHex(true),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(createGasLimit),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(Config.ChainId)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                from,
                contractAddress,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(initCode, programContext);
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            var gasUsed = program.TotalGasUsed;
            var returnData = program.ProgramResult.Result ?? Array.Empty<byte>();

            if (!program.ProgramResult.IsRevert && returnData.Length > 0)
            {
                gasUsed += (long)returnData.Length * TransactionProcessor.G_CODEDEPOSIT;
            }

            return new CallResult
            {
                Success = !program.ProgramResult.IsRevert,
                ReturnData = returnData,
                RevertReason = program.ProgramResult.GetRevertMessage(),
                GasUsed = gasUsed
            };
        }

        public virtual async Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, BigInteger blockNumber,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var createValue = value ?? BigInteger.Zero;
            var createGasLimit = gasLimit ?? 10_000_000;

            if (initCode == null || initCode.Length == 0)
            {
                return new CallResult { Success = true, ReturnData = Array.Empty<byte>(), GasUsed = 0 };
            }

            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            var executionStateService = new ExecutionStateService(dataService);

            var callerBalance = await dataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var contractAddress = ContractUtils.CalculateContractAddress(from, 0);

            var callInput = new CallInput
            {
                From = from,
                To = contractAddress,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(createValue),
                Data = initCode.ToHex(true),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(createGasLimit),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(Config.ChainId)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                from,
                contractAddress,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(initCode, programContext);
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            var gasUsed = program.TotalGasUsed;
            var returnData = program.ProgramResult.Result ?? Array.Empty<byte>();

            if (!program.ProgramResult.IsRevert && returnData.Length > 0)
            {
                gasUsed += (long)returnData.Length * TransactionProcessor.G_CODEDEPOSIT;
            }

            return new CallResult
            {
                Success = !program.ProgramResult.IsRevert,
                ReturnData = returnData,
                RevertReason = program.ProgramResult.GetRevertMessage(),
                GasUsed = gasUsed
            };
        }

        public virtual async Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, string from = null,
            BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = gasLimit ?? 10_000_000;

            var blockContext = await GetBlockContextForCallAsync();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var code = await _nodeDataService.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                return new AccessListResult { AccessList = new List<AccessListItem>(), GasUsed = 0 };
            }

            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var callInput = new CallInput
            {
                From = from,
                To = to,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(callValue),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(callGasLimit),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(Config.ChainId)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                from,
                to,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee,
                trackAccessList: true);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(code, programContext);
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            if (program.ProgramResult.IsRevert)
            {
                return new AccessListResult
                {
                    AccessList = programContext.GetAccessList(),
                    GasUsed = program.TotalGasUsed,
                    Error = program.ProgramResult.GetRevertMessage() ?? "execution reverted"
                };
            }

            return new AccessListResult
            {
                AccessList = programContext.GetAccessList(),
                GasUsed = program.TotalGasUsed
            };
        }

        public virtual async Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, BigInteger blockNumber,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = gasLimit ?? 10_000_000;

            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            var executionStateService = new ExecutionStateService(dataService);

            var code = await dataService.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                return new AccessListResult { AccessList = new List<AccessListItem>(), GasUsed = 0 };
            }

            var callerBalance = await dataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            var callInput = new CallInput
            {
                From = from,
                To = to,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(callValue),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(callGasLimit),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(Config.ChainId)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                from,
                to,
                (long)blockContext.BlockNumber,
                blockContext.Timestamp,
                blockContext.Coinbase,
                (long)blockContext.BaseFee,
                trackAccessList: true);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(code, programContext);
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            if (program.ProgramResult.IsRevert)
            {
                return new AccessListResult
                {
                    AccessList = programContext.GetAccessList(),
                    GasUsed = program.TotalGasUsed,
                    Error = program.ProgramResult.GetRevertMessage() ?? "execution reverted"
                };
            }

            return new AccessListResult
            {
                AccessList = programContext.GetAccessList(),
                GasUsed = program.TotalGasUsed
            };
        }

        public abstract Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx);
        public abstract Task<List<ISignedTransaction>> GetPendingTransactionsAsync();

        #region Tracing

        protected virtual async Task<TraceExecutionResult> PrepareAndExecuteTraceAsync(string txHash, bool traceEnabled = true)
        {
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

            var blockContext = new BlockContext
            {
                BlockNumber = block.BlockNumber,
                Timestamp = block.Timestamp,
                Coinbase = block.Coinbase ?? Config.Coinbase,
                Difficulty = block.Difficulty,
                GasLimit = block.GasLimit,
                BaseFee = block.BaseFee ?? 0,
                ChainId = Config.ChainId
            };

            IStateReader traceNodeDataService;
            if (_stateStore is IHistoricalStateProvider historyProvider)
            {
                var targetBlock = block.BlockNumber > 0 ? block.BlockNumber - 1 : 0;
                traceNodeDataService = new HistoricalNodeDataService(historyProvider, _stateStore, _blockStore, targetBlock);
            }
            else
            {
                traceNodeDataService = _nodeDataService;
            }

            var executionStateService = new ExecutionStateService(traceNodeDataService);

            if (receiptInfo.TransactionIndex > 0)
            {
                await ReplayPrecedingTransactionsAsync(
                    receiptInfo.BlockHash, receiptInfo.TransactionIndex, blockContext, executionStateService);
            }

            if (!executionStateService.ContainsInitialChainBalanceForAddress(from))
            {
                var callerBalance = await traceNodeDataService.GetBalanceAsync(from);
                executionStateService.SetInitialChainBalance(from, callerBalance);
            }

            var callInput = new CallInput
            {
                From = from,
                To = to,
                Value = new HexBigInteger(value),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new HexBigInteger(gasLimit),
                GasPrice = new HexBigInteger(0),
                ChainId = new HexBigInteger(Config.ChainId)
            };

            var code = await executionStateService.GetCodeAsync(to);

            if (code == null || code.Length == 0)
            {
                return new TraceExecutionResult
                {
                    CallInput = callInput,
                    StateService = executionStateService,
                    IsContractCreation = isContractCreation,
                    IsSimpleTransfer = true
                };
            }

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
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: traceEnabled);

            return new TraceExecutionResult
            {
                Program = program,
                CallInput = callInput,
                StateService = executionStateService,
                IsContractCreation = isContractCreation,
                IsSimpleTransfer = false
            };
        }

        private async Task ReplayPrecedingTransactionsAsync(
            byte[] blockHash,
            int targetTxIndex,
            BlockContext blockContext,
            ExecutionStateService executionStateService)
        {
            var blockTxs = await _transactionStore.GetByBlockHashAsync(blockHash);
            if (blockTxs == null || blockTxs.Count == 0)
                return;

            for (int i = 0; i < targetTxIndex && i < blockTxs.Count; i++)
            {
                var precedingTx = blockTxs[i];
                var txData = TransactionProcessor.GetTransactionData(precedingTx);
                var senderAddress = _txVerifier.GetSenderAddress(precedingTx);
                if (string.IsNullOrEmpty(senderAddress))
                    continue;

                if (!executionStateService.ContainsInitialChainBalanceForAddress(senderAddress))
                {
                    var senderBalance = await executionStateService.StateReader.GetBalanceAsync(senderAddress);
                    executionStateService.SetInitialChainBalance(senderAddress, senderBalance);
                }

                var isCreate = string.IsNullOrEmpty(txData.To) || txData.To == "0x";
                var ctx = new TransactionExecutionContext
                {
                    Mode = ExecutionMode.Transaction,
                    Sender = senderAddress,
                    To = isCreate ? null : txData.To,
                    Data = txData.Data,
                    Value = txData.Value,
                    GasLimit = txData.GasLimit,
                    GasPrice = txData.GasPrice,
                    MaxFeePerGas = txData.MaxFeePerGas ?? txData.GasPrice,
                    MaxPriorityFeePerGas = txData.MaxPriorityFeePerGas ?? BigInteger.Zero,
                    Nonce = txData.Nonce,
                    IsEip1559 = txData.MaxFeePerGas.HasValue,
                    IsType4Transaction = txData.AuthorisationList != null && txData.AuthorisationList.Count > 0,
                    IsContractCreation = isCreate,
                    BlockNumber = (long)blockContext.BlockNumber,
                    Timestamp = blockContext.Timestamp,
                    Coinbase = blockContext.Coinbase,
                    BaseFee = blockContext.BaseFee,
                    Difficulty = blockContext.Difficulty,
                    BlockGasLimit = blockContext.GasLimit,
                    ChainId = blockContext.ChainId,
                    ExecutionState = executionStateService,
                    TraceEnabled = false,
                    AccessList = txData.AccessList,
                    AuthorisationList = txData.AuthorisationList
                };

                await _executor.ExecuteAsync(ctx);
            }
        }

        public virtual async Task<OpcodeTraceResult> TraceTransactionAsync(
            string txHash,
            OpcodeTraceConfig config = null)
        {
            var result = await PrepareAndExecuteTraceAsync(txHash);

            if (result.IsSimpleTransfer)
            {
                return new OpcodeTraceResult
                {
                    Gas = 0,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<OpcodeTraceStep>()
                };
            }

            return TraceConverter.ConvertToOpcodeResult(result.Program, config);
        }

        public virtual async Task<CallTraceResult> TraceTransactionCallTracerAsync(string txHash)
        {
            var result = await PrepareAndExecuteTraceAsync(txHash, traceEnabled: false);

            if (result.IsSimpleTransfer)
            {
                return new CallTraceResult
                {
                    Type = "CALL",
                    From = result.CallInput.From,
                    To = result.CallInput.To,
                    Value = result.CallInput.Value ?? new HexBigInteger(0),
                    Gas = result.CallInput.Gas ?? new HexBigInteger(0),
                    GasUsed = new HexBigInteger(21000),
                    Input = "0x",
                    Output = "0x"
                };
            }

            return TraceConverter.ConvertToCallTraceResult(
                result.Program, result.CallInput, result.IsContractCreation);
        }

        public virtual async Task<PrestateTraceResult> TraceTransactionPrestateAsync(string txHash)
        {
            var result = await PrepareAndExecuteTraceAsync(txHash, traceEnabled: false);

            if (result.IsSimpleTransfer)
            {
                return new PrestateTraceResult
                {
                    Pre = new Dictionary<string, PrestateAccountInfo>(),
                    Post = new Dictionary<string, PrestateAccountInfo>()
                };
            }

            return TraceConverter.ConvertToPrestateResult(
                result.StateService, _nodeDataService);
        }

        public virtual async Task<OpcodeTraceResult> TraceCallAsync(
            CallInput callInput,
            OpcodeTraceConfig config = null,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var result = await ExecuteTraceCallAsync(callInput, stateOverrides);
            if (result == null)
            {
                return new OpcodeTraceResult
                {
                    Gas = 0,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<OpcodeTraceStep>()
                };
            }

            return TraceConverter.ConvertToOpcodeResult(result.Program, config);
        }

        public virtual async Task<OpcodeTraceResult> TraceCallAsync(
            CallInput callInput,
            BigInteger blockNumber,
            OpcodeTraceConfig config = null,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var result = await ExecuteTraceCallAsync(callInput, blockNumber, stateOverrides);
            if (result == null)
            {
                return new OpcodeTraceResult
                {
                    Gas = 0,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<OpcodeTraceStep>()
                };
            }

            return TraceConverter.ConvertToOpcodeResult(result.Program, config);
        }

        public virtual async Task<CallTraceResult> TraceCallCallTracerAsync(
            CallInput callInput,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var result = await ExecuteTraceCallAsync(callInput, stateOverrides);
            if (result == null)
            {
                return new CallTraceResult
                {
                    Type = "CALL",
                    From = callInput.From,
                    To = callInput.To,
                    Value = callInput.Value ?? new HexBigInteger(0),
                    Gas = callInput.Gas ?? new HexBigInteger(0),
                    GasUsed = new HexBigInteger(0),
                    Input = callInput.Data ?? "0x",
                    Output = "0x"
                };
            }

            return TraceConverter.ConvertToCallTraceResult(result.Program, result.CallInput, result.IsContractCreation);
        }

        public virtual async Task<CallTraceResult> TraceCallCallTracerAsync(
            CallInput callInput,
            BigInteger blockNumber,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var result = await ExecuteTraceCallAsync(callInput, blockNumber, stateOverrides);
            if (result == null)
            {
                return new CallTraceResult
                {
                    Type = "CALL",
                    From = callInput.From,
                    To = callInput.To,
                    Value = callInput.Value ?? new HexBigInteger(0),
                    Gas = callInput.Gas ?? new HexBigInteger(0),
                    GasUsed = new HexBigInteger(0),
                    Input = callInput.Data ?? "0x",
                    Output = "0x"
                };
            }

            return TraceConverter.ConvertToCallTraceResult(result.Program, result.CallInput, result.IsContractCreation);
        }

        public virtual async Task<PrestateTraceResult> TraceCallPrestateAsync(
            CallInput callInput,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var result = await ExecuteTraceCallAsync(callInput, stateOverrides);
            if (result == null)
            {
                return new PrestateTraceResult
                {
                    Pre = new Dictionary<string, PrestateAccountInfo>(),
                    Post = new Dictionary<string, PrestateAccountInfo>()
                };
            }

            return TraceConverter.ConvertToPrestateResult(
                result.Program.ProgramContext.ExecutionStateService,
                _nodeDataService);
        }

        public virtual async Task<PrestateTraceResult> TraceCallPrestateAsync(
            CallInput callInput,
            BigInteger blockNumber,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            var result = await ExecuteTraceCallAsync(callInput, blockNumber, stateOverrides);
            if (result == null)
            {
                return new PrestateTraceResult
                {
                    Pre = new Dictionary<string, PrestateAccountInfo>(),
                    Post = new Dictionary<string, PrestateAccountInfo>()
                };
            }

            return TraceConverter.ConvertToPrestateResult(
                result.Program.ProgramContext.ExecutionStateService,
                dataService);
        }

        private async Task<TraceExecutionResult> ExecuteTraceCallAsync(
            CallInput callInput,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var from = callInput.From ?? AddressUtil.ZERO_ADDRESS;
            var to = callInput.To;
            var callValue = callInput.Value?.Value ?? BigInteger.Zero;
            var callGasLimit = callInput.Gas?.Value ?? 10_000_000;
            var data = callInput.Data?.HexToByteArray();
            var isContractCreation = string.IsNullOrEmpty(to);

            var blockContext = await GetBlockContextForCallAsync();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var callerBalance = await _nodeDataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            if (stateOverrides != null)
            {
                ApplyStateOverrides(executionStateService, stateOverrides);
            }

            byte[] code;
            if (isContractCreation)
            {
                code = data;
                to = ContractUtils.CalculateContractAddress(from, 0);
            }
            else
            {
                code = await _nodeDataService.GetCodeAsync(to);
            }

            if (code == null || code.Length == 0)
                return null;

            var traceCallInput = new CallInput
            {
                From = from,
                To = isContractCreation ? null : to,
                Value = new HexBigInteger(callValue),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new HexBigInteger(callGasLimit),
                GasPrice = new HexBigInteger(0),
                ChainId = new HexBigInteger(Config.ChainId)
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
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            return new TraceExecutionResult
            {
                Program = program,
                CallInput = traceCallInput,
                StateService = executionStateService,
                IsContractCreation = isContractCreation
            };
        }

        private async Task<TraceExecutionResult> ExecuteTraceCallAsync(
            CallInput callInput,
            BigInteger blockNumber,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var from = callInput.From ?? AddressUtil.ZERO_ADDRESS;
            var to = callInput.To;
            var callValue = callInput.Value?.Value ?? BigInteger.Zero;
            var callGasLimit = callInput.Gas?.Value ?? 10_000_000;
            var data = callInput.Data?.HexToByteArray();
            var isContractCreation = string.IsNullOrEmpty(to);

            var dataService = GetNodeDataServiceAtBlock(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            var executionStateService = new ExecutionStateService(dataService);

            var callerBalance = await dataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            if (stateOverrides != null)
            {
                ApplyStateOverrides(executionStateService, stateOverrides);
            }

            byte[] code;
            if (isContractCreation)
            {
                code = data;
                to = ContractUtils.CalculateContractAddress(from, 0);
            }
            else
            {
                code = await dataService.GetCodeAsync(to);
            }

            if (code == null || code.Length == 0)
                return null;

            var traceCallInput = new CallInput
            {
                From = from,
                To = isContractCreation ? null : to,
                Value = new HexBigInteger(callValue),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new HexBigInteger(callGasLimit),
                GasPrice = new HexBigInteger(0),
                ChainId = new HexBigInteger(Config.ChainId)
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
            var simulator = new EVMSimulator(Config.GetHardforkConfig());
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            return new TraceExecutionResult
            {
                Program = program,
                CallInput = traceCallInput,
                StateService = executionStateService,
                IsContractCreation = isContractCreation
            };
        }

        protected virtual void ApplyStateOverrides(
            ExecutionStateService executionStateService,
            Dictionary<string, StateOverride> overrides)
        {
            foreach (var kvp in overrides)
            {
                var address = kvp.Key;
                var stateOverride = kvp.Value;
                var accountState = executionStateService.CreateOrGetAccountExecutionState(address);

                if (stateOverride.Balance != null)
                {
                    executionStateService.SetInitialChainBalance(address, stateOverride.Balance.Value);
                }

                if (!string.IsNullOrEmpty(stateOverride.Code))
                {
                    accountState.Code = stateOverride.Code.HexToByteArray();
                }

                if (stateOverride.State != null)
                {
                    foreach (var storageKvp in stateOverride.State)
                    {
                        var slot = storageKvp.Key.HexToBigInteger(false);
                        var storageValue = storageKvp.Value.HexToByteArray();
                        accountState.SetPreStateStorage(slot, storageValue);
                    }
                }

                if (stateOverride.StateDiff != null)
                {
                    foreach (var storageKvp in stateOverride.StateDiff)
                    {
                        var slot = storageKvp.Key.HexToBigInteger(false);
                        var storageValue = storageKvp.Value.HexToByteArray();
                        accountState.SetPreStateStorage(slot, storageValue);
                    }
                }
            }
        }

        #endregion

        protected virtual async Task<BlockContext> GetBlockContextForCallAsync()
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            return new BlockContext
            {
                BlockNumber = latestBlock?.BlockNumber ?? 0,
                Timestamp = latestBlock?.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = Config.Coinbase,
                GasLimit = Config.BlockGasLimit,
                BaseFee = Config.BaseFee,
                ChainId = Config.ChainId,
                Difficulty = 0
            };
        }

        protected IStateReader GetNodeDataServiceAtBlock(BigInteger blockNumber)
        {
            if (_stateStore is IHistoricalStateProvider historyProvider)
            {
                return new HistoricalNodeDataService(historyProvider, _stateStore, _blockStore, blockNumber);
            }
            return _nodeDataService;
        }

        protected virtual async Task<BlockContext> GetBlockContextAtBlockAsync(BigInteger blockNumber)
        {
            var block = await _blockStore.GetByNumberAsync(blockNumber);
            if (block != null)
            {
                return new BlockContext
                {
                    BlockNumber = block.BlockNumber,
                    Timestamp = block.Timestamp,
                    Coinbase = block.Coinbase ?? Config.Coinbase,
                    GasLimit = block.GasLimit,
                    BaseFee = block.BaseFee ?? Config.BaseFee,
                    ChainId = Config.ChainId,
                    Difficulty = block.Difficulty
                };
            }
            return await GetBlockContextForCallAsync();
        }
    }
}
