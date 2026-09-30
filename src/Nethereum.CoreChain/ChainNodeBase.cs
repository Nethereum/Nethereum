using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.CoreChain.Rpc;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase : IChainNode
    {
        protected readonly IBlockStore _blockStore;
        protected readonly ITransactionStore _transactionStore;
        protected readonly IUncleStore _uncleStore;
        protected readonly IReceiptStore _receiptStore;
        protected readonly ILogStore _logStore;
        protected readonly IStateStore _stateStore;
        protected readonly IFilterStore _filterStore;
        protected readonly ITrieNodeStore _trieNodeStore;
        protected readonly IBlobStore _blobStore;
        protected readonly IBlockAccessListStore _blockAccessListStore;
        protected readonly IStateReader _nodeDataService;
        protected readonly TransactionProcessor _transactionProcessor;
        protected readonly ITransactionVerificationAndRecovery _txVerifier;
        protected readonly TransactionExecutor _executor;
        protected readonly ILogger _logger;

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
            IUncleStore uncleStore = null,
            HardforkConfig hardforkConfig = null,
            IChainActivations activations = null,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory = null,
            ILogger logger = null)
            : this(
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                filterStore,
                transactionProcessor,
                txVerifier,
                blockAccessListStore: null,
                nodeDataService: nodeDataService,
                trieNodeStore: trieNodeStore,
                blobStore: blobStore,
                uncleStore: uncleStore,
                hardforkConfig: hardforkConfig,
                activations: activations,
                hardforkConfigFactory: hardforkConfigFactory,
                logger: logger)
        {
        }

        protected ChainNodeBase(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            IFilterStore filterStore,
            TransactionProcessor transactionProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            IBlockAccessListStore blockAccessListStore,
            IStateReader nodeDataService = null,
            ITrieNodeStore trieNodeStore = null,
            IBlobStore blobStore = null,
            IUncleStore uncleStore = null,
            HardforkConfig hardforkConfig = null,
            IChainActivations activations = null,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory = null,
            ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
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
            _uncleStore = uncleStore;
            _blockAccessListStore = blockAccessListStore;
            if (hardforkConfig == null) throw new ArgumentNullException(nameof(hardforkConfig));
            _staticHardforkConfig = hardforkConfig;
            _executor = new TransactionExecutor(hardforkConfig);
            _activations = activations;
            _hardforkConfigFactory = hardforkConfigFactory;
        }

        public abstract ChainConfig Config { get; }

        protected static readonly BigInteger DefaultCallGas = 10_000_000;

        protected virtual BigInteger ResolveCallGas(BigInteger? gasLimit)
        {
            var requested = gasLimit ?? DefaultCallGas;
            var cap = Config.RpcGasCap;
            return cap > 0 && requested > cap ? cap : requested;
        }

        public IBlockStore Blocks => _blockStore;
        public ITransactionStore Transactions => _transactionStore;
        public IUncleStore Uncles => _uncleStore;
        public IReceiptStore Receipts => _receiptStore;
        public ILogStore Logs => _logStore;
        public IStateStore State => _stateStore;
        public IFilterStore Filters => _filterStore;
        public ITrieNodeStore TrieNodes => _trieNodeStore;
        public IBlobStore BlobStore => _blobStore;

        public IBlockAccessListStore BlockAccessLists => _blockAccessListStore;

        private Services.IProofService _proofService;
        public virtual Services.IProofService ProofService =>
            _proofService ??= new Services.ProofService(_stateStore, _trieNodeStore);

        public abstract Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx);
        public abstract Task<List<ISignedTransaction>> GetPendingTransactionsAsync();


        private CallInput BuildTraceCallInput(ISignedTransaction tx)
        {
            var isContractCreation = tx.IsContractCreation();
            return new CallInput
            {
                From = RpcTransactionAddress.Normalize(_txVerifier.GetSenderAddress(tx)),
                To = isContractCreation ? null : RpcTransactionAddress.Normalize(tx.GetReceiverAddress()),
                Value = new HexBigInteger(tx.GetValue()),
                Data = tx.GetData()?.ToHex(true) ?? "0x",
                Gas = new HexBigInteger(tx.GetGasLimit()),
                GasPrice = new HexBigInteger(0),
                ChainId = new HexBigInteger(Config.ChainId)
            };
        }

        private async Task<Nethereum.EVM.TransactionExecutionResult> ExecuteBlockTxAsync(
            ISignedTransaction tx,
            BlockContext blockContext,
            ExecutionStateService executionStateService,
            bool traceEnabled)
        {
            var senderAddress = _txVerifier.GetSenderAddress(tx);
            if (string.IsNullOrEmpty(senderAddress))
                return null;

            if (!executionStateService.ContainsInitialChainBalanceForAddress(senderAddress))
            {
                var senderBalance = await executionStateService.StateReader.GetBalanceAsync(senderAddress);
                executionStateService.SetInitialChainBalance(senderAddress, senderBalance);
            }

            var ctx = TransactionContextFactory.From(tx, senderAddress, blockContext, executionStateService);
            ctx.TraceEnabled = traceEnabled;

            return await ResolveExecutor((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp).ExecuteAsync(ctx);
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

                if (!string.IsNullOrEmpty(stateOverride.Nonce))
                {
                    executionStateService.SetNonce(address, EvmUInt256.FromHex(stateOverride.Nonce));
                }

                if (stateOverride.State != null)
                {
                    accountState.ResetStorageForOverride();
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


    }
}
