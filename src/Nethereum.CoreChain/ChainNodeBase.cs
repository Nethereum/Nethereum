using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
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
        protected readonly INodeDataService _nodeDataService;
        protected readonly TransactionProcessor _transactionProcessor;
        protected readonly ITransactionVerificationAndRecovery _txVerifier;

        protected ChainNodeBase(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            IFilterStore filterStore,
            TransactionProcessor transactionProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            INodeDataService nodeDataService = null,
            ITrieNodeStore trieNodeStore = null)
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
        }

        public abstract ChainConfig Config { get; }
        public IBlockStore Blocks => _blockStore;
        public ITransactionStore Transactions => _transactionStore;
        public IReceiptStore Receipts => _receiptStore;
        public ILogStore Logs => _logStore;
        public IStateStore State => _stateStore;
        public IFilterStore Filters => _filterStore;
        public ITrieNodeStore TrieNodes => _trieNodeStore;

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
            return await _nodeDataService.GetTransactionCount(address);
        }

        public virtual async Task<byte[]> GetCodeAsync(string address)
        {
            return await _nodeDataService.GetCodeAsync(address);
        }

        public virtual async Task<byte[]> GetStorageAtAsync(string address, BigInteger slot)
        {
            return await _nodeDataService.GetStorageAtAsync(address, slot);
        }

        public virtual async Task<CallResult> CallAsync(string to, byte[] data, string from = null,
            BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? "0x0000000000000000000000000000000000000000";
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = gasLimit ?? 10_000_000;

            var blockContext = await GetBlockContextForCallAsync();
            var executionStateService = new ExecutionStateService(_nodeDataService);

            var code = await _nodeDataService.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                return new CallResult { Success = true, ReturnData = Array.Empty<byte>(), GasUsed = 0 };
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
                (long)blockContext.BaseFee);

            programContext.GasLimit = blockContext.GasLimit;
            programContext.Difficulty = blockContext.Difficulty;

            var program = new Program(code, programContext);
            var simulator = new EVMSimulator();
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            return new CallResult
            {
                Success = !program.ProgramResult.IsRevert,
                ReturnData = program.ProgramResult.Result ?? Array.Empty<byte>(),
                RevertReason = program.ProgramResult.GetRevertMessage(),
                GasUsed = program.TotalGasUsed
            };
        }

        public virtual async Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            from = from ?? "0x0000000000000000000000000000000000000000";
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
            var simulator = new EVMSimulator();
            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            var gasUsed = program.TotalGasUsed;
            var returnData = program.ProgramResult.Result ?? Array.Empty<byte>();

            if (!program.ProgramResult.IsRevert && returnData.Length > 0)
            {
                gasUsed += (BigInteger)returnData.Length * TransactionProcessor.G_CODEDEPOSIT;
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
            from = from ?? "0x0000000000000000000000000000000000000000";
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
            var simulator = new EVMSimulator();
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
    }
}
