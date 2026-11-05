using System.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    public sealed class BlockExecutor
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly IChainActivations _activations;

        public IChainActivations Activations => _activations;

        public IIncrementalStateRootCalculator StateRootCalculator => _stateRootCalculator;
        private readonly Func<HardforkName, ChainConfig> _chainConfigFactory;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly IIncrementalStateRootCalculator _stateRootCalculator;
        private readonly IRewardPolicy _rewardPolicy;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly ILogger<BlockExecutor>? _logger;
        private readonly Nethereum.Model.IBlockRootsProvider _blockRootsProvider;
        private readonly Func<BlockHeader, string>? _authorResolver;
        private readonly BlockValidationOptions _validation;
        private readonly BlockBodyValidator _bodyValidator;
        private readonly SenderRecoveryStep _senderRecovery;

        internal ITransactionVerificationAndRecovery SenderVerifier => _senderRecovery.Verifier;

        public BlockExecutor(
            IStateStore stateStore,
            IBlockStore blockStore,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IIncrementalStateRootCalculator stateRootCalculator,
            IRewardPolicy rewardPolicy,
            ITrieNodeStore trieNodeStore,
            ILogger<BlockExecutor>? logger = null,
            Nethereum.Model.IBlockRootsProvider? blockRootsProvider = null,
            Func<BlockHeader, string>? authorResolver = null)
            : this(stateStore, blockStore, activations, chainConfigFactory, hardforkConfigFactory,
                   stateRootCalculator, rewardPolicy, trieNodeStore, logger, blockRootsProvider,
                   authorResolver, new BlockValidationOptions())
        {
        }

        public BlockExecutor(
            IStateStore stateStore,
            IBlockStore blockStore,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IIncrementalStateRootCalculator stateRootCalculator,
            IRewardPolicy rewardPolicy,
            ITrieNodeStore trieNodeStore,
            ILogger<BlockExecutor>? logger,
            Nethereum.Model.IBlockRootsProvider? blockRootsProvider,
            Func<BlockHeader, string>? authorResolver,
            BlockValidationOptions validation)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _chainConfigFactory = chainConfigFactory ?? throw new ArgumentNullException(nameof(chainConfigFactory));
            _hardforkConfigFactory = hardforkConfigFactory ?? throw new ArgumentNullException(nameof(hardforkConfigFactory));
            _stateRootCalculator = stateRootCalculator ?? throw new ArgumentNullException(nameof(stateRootCalculator));
            _rewardPolicy = rewardPolicy ?? throw new ArgumentNullException(nameof(rewardPolicy));
            _trieNodeStore = trieNodeStore ?? throw new ArgumentNullException(nameof(trieNodeStore));
            _logger = logger;
            _blockRootsProvider = blockRootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _authorResolver = authorResolver;
            _validation = validation ?? throw new ArgumentNullException(nameof(validation));
            _senderRecovery = new SenderRecoveryStep(validation.TransactionVerifier);
            _bodyValidator = new BlockBodyValidator(
                blockStore, validation.UncleStore, activations, hardforkConfigFactory, _blockRootsProvider);
        }

        public async Task<BlockExecutionResult> ExecuteAsync(
            BlockHeader header,
            IReadOnlyList<TxEntry> txs,
            IList<BlockHeader>? uncles,
            IList<WithdrawalEntry>? withdrawals,
            BlockExecutionOptions options,
            CancellationToken ct = default)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (txs == null) throw new ArgumentNullException(nameof(txs));
            options ??= new BlockExecutionOptions();

            var validating = options.Role == BlockExecutionRole.Validating;
            var simulating = options.Role == BlockExecutionRole.Simulating;

            IStateStore stateForBlock = SelectStateStoreForBlock(options);
            var statsCollector = !simulating && !options.ReadOnly
                ? BlockStatsCollector.TryAttach(stateForBlock)
                : null;

            ulong blockNumber = (ulong)header.BlockNumber;
            ulong timestamp = (ulong)header.Timestamp;
            var fork = _activations.ResolveAt((long)blockNumber, timestamp);

            var carriesBlockAccessList = HeaderCarriesBlockAccessList(fork);
            var balRecorder = carriesBlockAccessList ? new BlockAccessListRecorder(stateForBlock) : null;

            var blockHardforkConfig = _hardforkConfigFactory(fork).AtBlock(_activations, (long)blockNumber, timestamp);

            BlockHeader? parentHeader = await ResolveParentHeaderAsync(header).ConfigureAwait(false);
            byte[]? preStateRoot = parentHeader?.StateRoot;

            if (validating)
            {
                var headerRefusal = RefuseInvalidHeader(header, parentHeader, fork, blockHardforkConfig);
                if (headerRefusal != null) return headerRefusal;

                var bodyRefusal = await RefuseInvalidBodyAsync(header, txs, uncles, withdrawals, fork).ConfigureAwait(false);
                if (bodyRefusal != null) return bodyRefusal;
            }

            var accumulated = new BlockAccumulation(txs.Count);
            var outcome = new BlockExecutionAccumulator();
            WitnessRecordingStateReader? witnessRecorder = null;

            try
            {
                witnessRecorder = ArmWitnessRecorder(options, stateForBlock);

                var chainConfig = _chainConfigFactory(fork);
                var hardforkConfig = statsCollector?.ObservePrecompiles(blockHardforkConfig) ?? blockHardforkConfig;
                var senders = BeginSenderRecovery(txs, options, simulating);
                var txProcessor = new TransactionProcessor(stateForBlock, _blockStore, chainConfig, _senderRecovery.Verifier, hardforkConfig,
                    eip158EmptyAccountPruning: fork >= HardforkName.SpuriousDragon);
                var blockContext = BuildBlockContext(header, chainConfig, fork, _authorResolver);

                statsCollector?.BeginExecution(_stateRootCalculator);

                await ApplyPreTransactionSystemCallsAsync(
                    stateForBlock, txProcessor, blockContext, header, fork, blockNumber, options, witnessRecorder, balRecorder, ct).ConfigureAwait(false);

                if (CanWarmStartFromParentRoot(options, preStateRoot))
                {
                    await WarmStartStateRootCalculatorFromParentAsync(preStateRoot).ConfigureAwait(false);
                }

                StartTriePathPrefetch(options, stateForBlock, hardforkConfig, simulating, preStateRoot);
                ExecutionCostBaseline cost;
                try
                {
                    var run = new BlockRun(
                        header, txs, uncles, withdrawals, options, stateForBlock, fork, hardforkConfig, chainConfig,
                        txProcessor, blockContext, witnessRecorder, balRecorder, accumulated, outcome, senders);
                    cost = await ExecuteTransactionsAndPostStepsAsync(run, ct).ConfigureAwait(false);
                }
                finally
                {
                    options.Prefetch?.EndTriePathsBeforeRoot();
                }

                if (!options.ReadOnly || simulating)
                {
                    outcome.Settlement = await SettlePostStateAndDecideCommitAsync(
                        header, accumulated.Receipts, outcome.BlockBloom, accumulated.Capacity, outcome.BlockAccessList, carriesBlockAccessList,
                        validating, simulating, accumulated.ContainsInvalidTransaction, outcome.BlockAccessListGasLimitExceeded,
                        txs.Count, accumulated.CumulativeGasUsed, cost, statsCollector)
                        .ConfigureAwait(false);
                }

                outcome.WitnessBytes = BlockWitnessBuilder.BuildBlockWitness(options, witnessRecorder, txs, header, chainConfig, fork,
                    _activations.BlobParametersAt((long)blockNumber, timestamp), withdrawals, options.ParentBeaconBlockRoot ?? header.ParentBeaconBlockRoot, preStateRoot);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                outcome.Exception = ex;
                outcome.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Block execution failed at block {BlockNumber}", header.BlockNumber);
            }
            finally
            {
                statsCollector?.Detach();
            }

            return BuildExecutionResult(outcome, accumulated, header, fork, preStateRoot, withdrawals, validating);
        }

        private async Task<ExecutionCostBaseline> ExecuteTransactionsAndPostStepsAsync(BlockRun run, CancellationToken ct)
        {
            var txs = run.Transactions;
            var options = run.Options;
            var accumulated = run.Accumulated;
            var outcome = run.Outcome;
            var validating = options.Role == BlockExecutionRole.Validating;
            var simulating = options.Role == BlockExecutionRole.Simulating;

            var cost = ExecutionCostBaseline.CaptureAndStartExecutionTimer(_stateStore);
            if (run.Senders != null) await run.Senders.Completed.ConfigureAwait(false);
            for (int i = 0; i < txs.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = txs[i];
                if (!simulating) entry = WithRecoveredSender(entry, i, run.Senders);
                if (simulating)
                {
                    var runningNonce = await SimulateEntryResolver.ResolveSimulateRunningNonceAsync(run.StateForBlock, entry).ConfigureAwait(false);
                    entry = SimulateEntryResolver.ResolveSimulateEntry(
                        entry, run.ChainConfig.RpcGasCap, run.Header.GasLimit,
                        (long)accumulated.CumulativeGasUsed, options.SimulateGlobalGasUsedBefore,
                        run.BlockContext.ChainId, runningNonce);
                }
                outcome.ExecutingTransactionIndex = i;
                var txResult = await ExecuteTransactionAtIndexAsync(
                    run.TxProcessor, entry, run.BlockContext, i, accumulated.CumulativeGasUsed,
                    run.WitnessRecorder, options, run.BalRecorder, accumulated.Capacity).ConfigureAwait(false);
                outcome.ExecutingTransactionIndex = -1;

                await StampIntermediatePostStateRootWhenRequiredAsync(
                    txResult, run.HardforkConfig, options).ConfigureAwait(false);

                accumulated.Add(entry.Tx, txResult, validating,
                    run.HardforkConfig.IntrinsicGasRules.StateGasActive);
            }

            outcome.BlockBloom = accumulated.BlockBloom;

            var postExecutionIndex = (ulong)(txs.Count + 1);

            var executionRequests = await CollectExecutionRequestsAsync(
                run.TxProcessor, run.Fork, run.BlockContext, run.WitnessRecorder, run.BalRecorder, postExecutionIndex,
                accumulated.Logs).ConfigureAwait(false);
            outcome.ComputedRequestsHash = executionRequests.Commitment();
            outcome.ExecutionRequests = executionRequests.NonEmptyRequests();

            (outcome.WithdrawalsCredited, outcome.MinerRewardCredited) = await ApplyWithdrawalsAndRewardsAsync(
                run.Header, run.Withdrawals, run.Uncles, run.StateForBlock, run.Fork, ct, run.BalRecorder, postExecutionIndex).ConfigureAwait(false);

            if (run.BalRecorder != null)
            {
                (outcome.BlockAccessList, outcome.BlockAccessListGasLimitExceeded) =
                    BuildBlockAccessListAndApplyItsSizeRule(run.BalRecorder, run.Header);
            }

            outcome.BlockAccessListMalformed = DeclaredBlockAccessListIsMalformed(options, txs.Count);

            return cost;
        }

        private sealed class BlockRun
        {
            public BlockRun(
                BlockHeader header,
                IReadOnlyList<TxEntry> transactions,
                IList<BlockHeader>? uncles,
                IList<WithdrawalEntry>? withdrawals,
                BlockExecutionOptions options,
                IStateStore stateForBlock,
                HardforkName fork,
                HardforkConfig hardforkConfig,
                ChainConfig chainConfig,
                TransactionProcessor txProcessor,
                BlockContext blockContext,
                WitnessRecordingStateReader? witnessRecorder,
                BlockAccessListRecorder? balRecorder,
                BlockAccumulation accumulated,
                BlockExecutionAccumulator outcome,
                RecoveredSenders? senders)
            {
                Header = header;
                Transactions = transactions;
                Uncles = uncles;
                Withdrawals = withdrawals;
                Options = options;
                StateForBlock = stateForBlock;
                Fork = fork;
                HardforkConfig = hardforkConfig;
                ChainConfig = chainConfig;
                TxProcessor = txProcessor;
                BlockContext = blockContext;
                WitnessRecorder = witnessRecorder;
                BalRecorder = balRecorder;
                Accumulated = accumulated;
                Outcome = outcome;
                Senders = senders;
            }

            public BlockHeader Header { get; }
            public IReadOnlyList<TxEntry> Transactions { get; }
            public IList<BlockHeader>? Uncles { get; }
            public IList<WithdrawalEntry>? Withdrawals { get; }
            public BlockExecutionOptions Options { get; }
            public IStateStore StateForBlock { get; }
            public HardforkName Fork { get; }
            public HardforkConfig HardforkConfig { get; }
            public ChainConfig ChainConfig { get; }
            public TransactionProcessor TxProcessor { get; }
            public BlockContext BlockContext { get; }
            public WitnessRecordingStateReader? WitnessRecorder { get; }
            public BlockAccessListRecorder? BalRecorder { get; }
            public BlockAccumulation Accumulated { get; }
            public BlockExecutionAccumulator Outcome { get; }
            public RecoveredSenders? Senders { get; }
        }

        private sealed class BlockExecutionAccumulator
        {
            public byte[]? BlockBloom;
            public byte[]? ComputedRequestsHash;
            public IReadOnlyList<byte[]>? ExecutionRequests;
            public BigInteger MinerRewardCredited;
            public int WithdrawalsCredited;
            public bool BlockAccessListGasLimitExceeded;
            public bool BlockAccessListMalformed;
            public List<AccountChanges>? BlockAccessList;
            public BlockSettlementOutcome? Settlement;
            public byte[]? WitnessBytes;
            public Exception? Exception;
            public string? ErrorMessage;
            public int ExecutingTransactionIndex = -1;
        }

        private BlockExecutionResult BuildExecutionResult(
            BlockExecutionAccumulator outcome,
            BlockAccumulation accumulated,
            BlockHeader header,
            HardforkName fork,
            byte[]? preStateRoot,
            IList<WithdrawalEntry>? withdrawals,
            bool validating) =>
            new BlockExecutionResult
            {
                Fork = fork,
                PreStateRoot = preStateRoot,
                PostStateRoot = outcome.Settlement?.PostStateRoot,
                Receipts = accumulated.Receipts,
                Logs = accumulated.Logs,
                BlockBloom = outcome.BlockBloom,
                ComputedRequestsHash = outcome.ComputedRequestsHash,
                ExecutionRequests = outcome.ExecutionRequests,
                RequestsHashMismatch = HeaderCommitments.RequestsHashDoesNotMatchTheHeader(header, outcome.ComputedRequestsHash),
                WitnessBytes = outcome.WitnessBytes,
                MinerRewardCredited = outcome.MinerRewardCredited,
                WithdrawalsCredited = outcome.WithdrawalsCredited,
                StateRootMismatch = outcome.Settlement?.StateRootMismatch ?? false,
                GasUsed = accumulated.Capacity.HeaderGasUsed,
                BlockExecutionGasUsed = accumulated.Capacity.ExecutionGasUsed,
                BlockStateGasUsed = accumulated.Capacity.StateGasUsed,
                ReceiptsRootMismatch = outcome.Settlement?.ReceiptsRootMismatch ?? false,
                LogsBloomMismatch = outcome.Settlement?.LogsBloomMismatch ?? false,
                GasUsedMismatch = outcome.Settlement?.GasUsedMismatch ?? false,
                BlobGasUsedMismatch = outcome.Settlement?.BlobGasUsedMismatch ?? false,
                BlockAccessListHashMismatch = outcome.Settlement?.BlockAccessListHashMismatch ?? false,
                Stats = outcome.Settlement?.Stats,
                BlockAccessListGasLimitExceeded = outcome.BlockAccessListGasLimitExceeded,
                BlockAccessListMalformed = outcome.BlockAccessListMalformed,
                ContainsInvalidTransaction = accumulated.ContainsInvalidTransaction,
                BlockAccessList = outcome.BlockAccessList,
                Exception = outcome.Exception,
                ErrorMessage = outcome.ErrorMessage,
                FailedTransactionIndex = outcome.Exception == null ? -1 : outcome.ExecutingTransactionIndex
            };

        /// <summary>
        /// EIP-7928 §Engine API: engine_newPayloadV5 "Returns INVALID if access list is malformed
        /// or doesn't match". A recomputed hash answers only the second half; a list that is
        /// out of order or carries a duplicate account is malformed whatever it hashes to.
        /// </summary>
        private static bool DeclaredBlockAccessListIsMalformed(BlockExecutionOptions options, int transactionCount)
        {
            var declared = options.DeclaredBlockAccessList;
            if (declared == null) return false;

            return !BlockAccessListStructureRule
                .FindViolation(declared, transactionCount)
                .IsWellFormed;
        }

        private void StartTriePathPrefetch(
            BlockExecutionOptions options, IStateStore stateForBlock, HardforkConfig hardforkConfig, bool simulating,
            byte[]? preStateRoot)
        {
            if (options.Prefetch == null || simulating || options.ReadOnly) return;
            if (hardforkConfig.ReceiptConstruction.RequiresIntermediatePostStateRoot) return;
            options.Prefetch.StartTriePaths(stateForBlock, preStateRoot, _stateRootCalculator);
        }

        private IStateStore SelectStateStoreForBlock(BlockExecutionOptions options) =>
            options.Role == BlockExecutionRole.Simulating
                ? _stateStore
                : options.ReadOnly
                    ? new StateLayer().Stores.WitnessCapture(_stateStore)
                    : _stateStore;

        private static bool HeaderCarriesBlockAccessList(HardforkName fork) =>
            Model.Codecs.BlockHeaderCodecs.ForFork(fork).CarriesBlockAccessList;

        private BlockExecutionResult? RefuseInvalidHeader(
            BlockHeader header, BlockHeader? parentHeader, HardforkName fork, HardforkConfig hardforkConfig)
        {
            if (parentHeader == null)
                return header.BlockNumber.IsZero ? null : Refusal(fork, BlockValidityCheck.UnknownParent);

            var timestamp = unchecked((ulong)header.Timestamp);
            var verdict = HeaderValidator.Validate(
                header, parentHeader, fork, hardforkConfig, SealRulesFor(fork),
                _validation.UnixClock?.Invoke(),
                HeaderValidator.RequiresDaoExtraData(_activations, (long)(ulong)header.BlockNumber, timestamp),
                feeFreeBaseFee: _validation.FeeFreeBaseFee);
            return verdict is BlockValidityCheck failed ? Refusal(fork, failed) : null;
        }

        private SealRules SealRulesFor(HardforkName fork) =>
            _validation.SealedByClique ? SealRules.Clique
            : fork >= HardforkName.Paris ? SealRules.ProofOfStake
            : SealRules.Ethash;

        private static BlockExecutionResult Refusal(HardforkName fork, BlockValidityCheck failed) =>
            new BlockExecutionResult
            {
                Fork = fork,
                PreStateRoot = null,
                PostStateRoot = null,
                Receipts = Array.Empty<TransactionExecutionResult>(),
                Logs = Array.Empty<Log>(),
                GasUsed = 0,
                UnknownParent = failed == BlockValidityCheck.UnknownParent,
                BlockNumberMismatch = failed == BlockValidityCheck.BlockNumber,
                TimestampNotAfterParent = failed == BlockValidityCheck.Timestamp,
                FutureBlock = failed == BlockValidityCheck.FutureBlock,
                ExtraDataTooLong = failed == BlockValidityCheck.ExtraDataSize,
                DaoExtraDataMismatch = failed == BlockValidityCheck.DaoExtraData,
                GasLimitTooBig = failed == BlockValidityCheck.GasLimitTooBig,
                DifficultyMismatch = failed == BlockValidityCheck.Difficulty,
                NonceInvalid = failed == BlockValidityCheck.Nonce,
                UnclesHashMismatch = failed == BlockValidityCheck.UnclesHash,
                UnclesInvalid = failed == BlockValidityCheck.Uncles,
                WithdrawalsRootMismatch = failed == BlockValidityCheck.WithdrawalsRoot,
                TransactionsRootMismatch = failed == BlockValidityCheck.TransactionsRoot,
                WithdrawalsRootPresenceMismatch = failed == BlockValidityCheck.WithdrawalsRootPresence,
                ParentBeaconRootPresenceMismatch = failed == BlockValidityCheck.ParentBeaconRootPresence,
                BlockAccessListHashPresenceMismatch = failed == BlockValidityCheck.BlockAccessListHashPresence,
                SlotNumberPresenceMismatch = failed == BlockValidityCheck.SlotNumberPresence,
                GasCapacityExceeded = failed == BlockValidityCheck.GasCapacity,
                BlobGasCapacityExceeded = failed == BlockValidityCheck.BlobGasCapacity,
                BlobFieldFormatMismatch = failed == BlockValidityCheck.BlobFieldFormat,
                BaseFeeMismatch = failed == BlockValidityCheck.BaseFee,
                ExcessBlobGasMismatch = failed == BlockValidityCheck.ExcessBlobGas,
                GasLimitBoundViolated = failed == BlockValidityCheck.GasLimitBound,
                BlockRlpSizeExceeded = failed == BlockValidityCheck.BlockRlpSize
            };

        public async Task<BlockExecutionResult?> ValidateStructureAsync(
            BlockHeader header, IReadOnlyList<TxEntry> txs, IList<BlockHeader>? uncles,
            IList<WithdrawalEntry>? withdrawals, IBlockStore blocks)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (txs == null) throw new ArgumentNullException(nameof(txs));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));

            ulong blockNumber = (ulong)header.BlockNumber;
            ulong timestamp = (ulong)header.Timestamp;
            var fork = _activations.ResolveAt((long)blockNumber, timestamp);
            var hardforkConfig = _hardforkConfigFactory(fork).AtBlock(_activations, (long)blockNumber, timestamp);

            BlockHeader? parentHeader = header.ParentHash == null || header.ParentHash.Length == 0
                ? null
                : await blocks.GetByHashAsync(header.ParentHash).ConfigureAwait(false);

            var headerRefusal = RefuseInvalidHeader(header, parentHeader, fork, hardforkConfig);
            if (headerRefusal != null) return headerRefusal;

            var bodyValidator = new BlockBodyValidator(
                blocks, _validation.UncleStore, _activations, _hardforkConfigFactory, _blockRootsProvider);
            return await RefuseInvalidBodyAsync(header, txs, uncles, withdrawals, fork, bodyValidator).ConfigureAwait(false);
        }

        private Task<BlockExecutionResult?> RefuseInvalidBodyAsync(
            BlockHeader header, IReadOnlyList<TxEntry> txs, IList<BlockHeader>? uncles,
            IList<WithdrawalEntry>? withdrawals, HardforkName fork) =>
            RefuseInvalidBodyAsync(header, txs, uncles, withdrawals, fork, _bodyValidator);

        private async Task<BlockExecutionResult?> RefuseInvalidBodyAsync(
            BlockHeader header, IReadOnlyList<TxEntry> txs, IList<BlockHeader>? uncles,
            IList<WithdrawalEntry>? withdrawals, HardforkName fork, BlockBodyValidator bodyValidator)
        {
            var transactions = new List<ISignedTransaction>(txs.Count);
            foreach (var entry in txs) transactions.Add(entry.Tx);

            var verdict = await bodyValidator.ValidateAsync(
                header, transactions, uncles,
                withdrawals == null ? null : ToModelWithdrawals(withdrawals),
                fork, SealRulesFor(fork), _validation.UnixClock?.Invoke()).ConfigureAwait(false);
            return verdict is BlockValidityCheck failed ? Refusal(fork, failed) : null;
        }

        public static ulong ExpectedExcessBlobGas(BlockHeader? parentHeader, HardforkConfig hardforkConfig)
        {
            ulong parentExcessBlobGas = parentHeader?.ExcessBlobGas is long parentExcess
                ? unchecked((ulong)parentExcess)
                : 0UL;
            ulong parentBlobGasUsed = parentHeader?.BlobGasUsed is long parentUsed
                ? unchecked((ulong)parentUsed)
                : 0UL;
            ulong targetBlobGasPerBlock = (ulong)hardforkConfig.TargetBlobsPerBlock * BlobGasCalculator.GAS_PER_BLOB;

            var blobRule = hardforkConfig.IntrinsicGasRules.Blob;
            Eip7918ReservePriceInputs? reservePrice = blobRule.AppliesReservePrice
                ? new Eip7918ReservePriceInputs(
                    parentHeader?.BaseFee ?? EvmUInt256.Zero,
                    hardforkConfig.MaxBlobsPerBlock,
                    hardforkConfig.TargetBlobsPerBlock,
                    blobRule.BaseFeeUpdateFraction)
                : null;

            return BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas, parentBlobGasUsed, targetBlobGasPerBlock, reservePrice);
        }

        private WitnessRecordingStateReader? ArmWitnessRecorder(
            BlockExecutionOptions options, IStateStore stateForBlock) =>
            options.CaptureWitness
                ? new WitnessRecordingStateReader(new StateStoreNodeDataService(stateForBlock, _blockStore))
                : null;

        private static bool CanWarmStartFromParentRoot(BlockExecutionOptions options, byte[]? preStateRoot) =>
            !options.ReadOnly && preStateRoot != null && preStateRoot.Length > 0;

        private Task<byte[]> WarmStartStateRootCalculatorFromParentAsync(byte[]? preStateRoot) =>
            _stateRootCalculator.ComputeStateRootWithoutPersistAsync(preStateRoot);

        private RecoveredSenders? BeginSenderRecovery(IReadOnlyList<TxEntry> txs, BlockExecutionOptions options, bool simulating)
        {
            if (simulating) return null;
            return options.Prefetch?.Senders ?? _senderRecovery.Begin(TransactionsWithoutSender(txs));
        }

        private static ISignedTransaction?[] TransactionsWithoutSender(IReadOnlyList<TxEntry> txs)
        {
            var needing = new ISignedTransaction?[txs.Count];
            for (var i = 0; i < needing.Length; i++)
                if (txs[i].CachedSender == null) needing[i] = txs[i].Tx;
            return needing;
        }

        private static TxEntry WithRecoveredSender(TxEntry entry, int index, RecoveredSenders? senders)
        {
            if (entry.CachedSender != null || senders == null) return entry;
            var sender = senders.TryGetSender(index, entry.Tx);
            return sender == null ? entry : new TxEntry(entry.Tx, sender, entry.SimulateCall);
        }

        private static Task<TransactionExecutionResult> ExecuteTransactionAtIndexAsync(
            TransactionProcessor txProcessor,
            TxEntry entry,
            BlockContext blockContext,
            int index,
            BigInteger cumulativeGasUsed,
            IStateReader? stateReaderForExecution,
            BlockExecutionOptions options,
            BlockAccessListRecorder? balRecorder,
            Nethereum.EVM.Gas.BlockGasCapacity capacity) =>
            options.Role == BlockExecutionRole.Simulating
                ? txProcessor.ExecuteSimulatedCallAsync(
                    entry,
                    blockContext,
                    index,
                    (long)cumulativeGasUsed,
                    stateReaderForExecution,
                    options.SimulateCallOptions!,
                    balRecorder,
                    capacity)
                : txProcessor.ExecuteTransactionAsync(
                    entry.Tx,
                    blockContext,
                    index,
                    (long)cumulativeGasUsed,
                    entry.CachedSender,
                    stateReaderForExecution,
                    traceEnabled: options.TraceTxIndex == index,
                    balRecorder: balRecorder,
                    blockAccessIndex: (ulong)(index + 1),
                    blockGasCapacity: capacity);

        private async Task StampIntermediatePostStateRootWhenRequiredAsync(
            TransactionExecutionResult txResult, HardforkConfig hardforkConfig, BlockExecutionOptions options)
        {
            if (!hardforkConfig.ReceiptConstruction.RequiresIntermediatePostStateRoot) return;
            if (options.ReadOnly) return;
            if (txResult.Receipt == null) return;

            txResult.Receipt.PostStateOrStatus = await _stateRootCalculator
                .ComputeStateRootWithoutPersistAsync(null).ConfigureAwait(false);
        }

        private static byte[]? PublishBlockBloomOnlyWhenAReceiptExists(bool anyReceipt, byte[] combinedBloom) =>
            anyReceipt ? combinedBloom : null;

        private static (List<AccountChanges> List, bool ExceedsBlockGasLimit) BuildBlockAccessListAndApplyItsSizeRule(
            BlockAccessListRecorder balRecorder, BlockHeader header)
        {
            var blockAccessList = balRecorder.Build();
            return (blockAccessList,
                BlockAccessListSizeRule.ExceedsBlockGasLimit(blockAccessList, header.GasLimit));
        }

        private readonly struct ExecutionCostBaseline
        {
            private readonly int _gc1;
            private readonly int _gc2;
            private readonly long _allocatedBytes;
            private readonly Nethereum.CoreChain.Storage.IStateReadStats? _readStats;
            private readonly long _accountReads;
            private readonly long _storageReads;

            public System.Diagnostics.Stopwatch ExecutionTimer { get; }

            private ExecutionCostBaseline(
                int gc1, int gc2, long allocatedBytes,
                Nethereum.CoreChain.Storage.IStateReadStats? readStats,
                long accountReads, long storageReads, System.Diagnostics.Stopwatch executionTimer)
            {
                _gc1 = gc1;
                _gc2 = gc2;
                _allocatedBytes = allocatedBytes;
                _readStats = readStats;
                _accountReads = accountReads;
                _storageReads = storageReads;
                ExecutionTimer = executionTimer;
            }

            public static ExecutionCostBaseline CaptureAndStartExecutionTimer(IStateStore stateStore)
            {
                var gc2 = System.GC.CollectionCount(2);
                var gc1 = System.GC.CollectionCount(1);
                var allocated = System.GC.GetTotalAllocatedBytes(false);
                var readStats = stateStore as Nethereum.CoreChain.Storage.IStateReadStats;
                return new ExecutionCostBaseline(
                    gc1, gc2, allocated, readStats,
                    readStats?.AccountReads ?? 0, readStats?.StorageReads ?? 0,
                    System.Diagnostics.Stopwatch.StartNew());
            }

            public int Gc1Collections => System.GC.CollectionCount(1) - _gc1;
            public int Gc2Collections => System.GC.CollectionCount(2) - _gc2;
            public long AllocatedMegabytes => (System.GC.GetTotalAllocatedBytes(false) - _allocatedBytes) / (1024 * 1024);
            public long AccountReads => (_readStats?.AccountReads ?? 0) - _accountReads;
            public long StorageReads => (_readStats?.StorageReads ?? 0) - _storageReads;
        }

        private readonly struct BlockSettlementOutcome
        {
            public BlockSettlementOutcome(
                byte[]? postStateRoot,
                bool stateRootMismatch,
                bool receiptsRootMismatch,
                bool logsBloomMismatch,
                bool gasUsedMismatch,
                bool blobGasUsedMismatch,
                bool blockAccessListHashMismatch,
                BlockExecutionStats? stats = null)
            {
                PostStateRoot = postStateRoot;
                StateRootMismatch = stateRootMismatch;
                ReceiptsRootMismatch = receiptsRootMismatch;
                LogsBloomMismatch = logsBloomMismatch;
                GasUsedMismatch = gasUsedMismatch;
                BlobGasUsedMismatch = blobGasUsedMismatch;
                BlockAccessListHashMismatch = blockAccessListHashMismatch;
                Stats = stats;
            }

            public BlockExecutionStats? Stats { get; }

            public BlockSettlementOutcome WithStats(BlockExecutionStats? stats) =>
                new BlockSettlementOutcome(
                    PostStateRoot, StateRootMismatch, ReceiptsRootMismatch, LogsBloomMismatch,
                    GasUsedMismatch, BlobGasUsedMismatch, BlockAccessListHashMismatch, stats);

            public byte[]? PostStateRoot { get; }
            public bool StateRootMismatch { get; }
            public bool ReceiptsRootMismatch { get; }
            public bool LogsBloomMismatch { get; }
            public bool GasUsedMismatch { get; }
            public bool BlobGasUsedMismatch { get; }
            public bool BlockAccessListHashMismatch { get; }
        }

        private async Task<BlockSettlementOutcome> SettlePostStateAndDecideCommitAsync(
            BlockHeader header,
            List<TransactionExecutionResult> receipts,
            byte[]? blockBloom,
            Nethereum.EVM.Gas.BlockGasCapacity capacity,
            List<AccountChanges>? blockAccessList,
            bool carriesBlockAccessList,
            bool validating,
            bool simulating,
            bool containsInvalidTransaction,
            bool blockAccessListGasLimitExceeded,
            int txCount,
            BigInteger cumulativeGasUsed,
            ExecutionCostBaseline cost,
            BlockStatsCollector? statsCollector)
        {
            cost.ExecutionTimer.Stop();
            statsCollector?.EndExecution();
            var swRoot = System.Diagnostics.Stopwatch.StartNew();
            var postStateRoot = await _stateRootCalculator
                .ComputeStateRootWithoutPersistAsync(null).ConfigureAwait(false);
            swRoot.Stop();
            var stateRootMismatch = header.StateRoot != null
                && postStateRoot != null
                && !ByteUtil.AreEqual(postStateRoot, header.StateRoot);

            bool receiptsRootMismatch = false;
            bool logsBloomMismatch = false;
            bool gasUsedMismatch = false;
            bool blobGasUsedMismatch = false;
            bool blockAccessListHashMismatch = false;
            if (validating)
            {
                var commitments = HeaderCommitments.CompareHeaderCommitments(
                    _blockRootsProvider, header, receipts, blockBloom, capacity, blockAccessList, carriesBlockAccessList);

                receiptsRootMismatch = commitments.ReceiptsRootMismatch;
                logsBloomMismatch = commitments.LogsBloomMismatch;
                gasUsedMismatch = commitments.GasUsedMismatch;
                blobGasUsedMismatch = commitments.BlobGasUsedMismatch;
                blockAccessListHashMismatch = commitments.BlockAccessListHashMismatch;
            }

            var outcome = new BlockSettlementOutcome(
                postStateRoot, stateRootMismatch, receiptsRootMismatch, logsBloomMismatch,
                gasUsedMismatch, blobGasUsedMismatch, blockAccessListHashMismatch);

            var blockIsRefused =
                BlockIsRefused(outcome, blockAccessListGasLimitExceeded, containsInvalidTransaction);
            statsCollector?.EndValidation();
            var swPersist = System.Diagnostics.Stopwatch.StartNew();
            if (simulating || blockIsRefused)
                _stateRootCalculator.DiscardPendingState();
            else
                await _stateRootCalculator.PersistPendingStateAsync().ConfigureAwait(false);
            swPersist.Stop();
            _logger?.LogInformation(
                "block.exec.timing block={BlockNumber} txs={TxCount} gas={Gas} exec_ms={ExecMs:F3} root_ms={RootMs:F3} persist_ms={PersistMs:F3} gc1={Gc1} gc2={Gc2} alloc_mb={AllocMb} acct_reads={AcctReads} slot_reads={SlotReads}",
                header.BlockNumber, txCount, (long)cumulativeGasUsed,
                cost.ExecutionTimer.Elapsed.TotalMilliseconds, swRoot.Elapsed.TotalMilliseconds, swPersist.Elapsed.TotalMilliseconds,
                cost.Gc1Collections, cost.Gc2Collections, cost.AllocatedMegabytes,
                cost.AccountReads,
                cost.StorageReads);

            return outcome.WithStats(statsCollector?.Complete(_stateRootCalculator, txCount, (long)cumulativeGasUsed));
        }

        private static bool BlockIsRefused(
            in BlockSettlementOutcome outcome,
            bool blockAccessListGasLimitExceeded,
            bool containsInvalidTransaction) =>
            outcome.StateRootMismatch || outcome.ReceiptsRootMismatch
            || outcome.LogsBloomMismatch || outcome.GasUsedMismatch
            || outcome.BlobGasUsedMismatch || outcome.BlockAccessListHashMismatch
            || blockAccessListGasLimitExceeded
            || containsInvalidTransaction;

        private async Task ApplyPreTransactionSystemCallsAsync(
            IStateStore stateForBlock,
            TransactionProcessor txProcessor,
            BlockContext blockContext,
            BlockHeader header,
            HardforkName fork,
            ulong blockNumber,
            BlockExecutionOptions options,
            WitnessRecordingStateReader? witnessRecorder,
            BlockAccessListRecorder? balRecorder,
            CancellationToken ct)
        {
            const ulong preExecutionIndex = 0;

            var parentBeaconBlockRoot = options.ParentBeaconBlockRoot ?? header.ParentBeaconBlockRoot;
            if (fork >= HardforkName.Cancun && parentBeaconBlockRoot != null)
            {
                await txProcessor.ExecuteSystemCallAsync(
                    Eip4788Constants.BeaconRootsAddress,
                    blockContext,
                    witnessRecorder,
                    balRecorder,
                    preExecutionIndex,
                    PadTo32(parentBeaconBlockRoot)).ConfigureAwait(false);
            }

            if (fork >= HardforkName.Prague
                && header.BlockNumber > 0
                && header.ParentHash != null && header.ParentHash.Length == 32)
            {
                await txProcessor.ExecuteSystemCallAsync(
                    Eip2935Constants.HistoryStorageAddress,
                    blockContext,
                    witnessRecorder,
                    balRecorder,
                    preExecutionIndex,
                    header.ParentHash).ConfigureAwait(false);
            }

            if (_activations is Nethereum.EVM.MainnetChainActivations &&
                (long)blockNumber == Nethereum.EVM.MainnetChainActivations.DaoForkBlock)
            {
                await ApplyDaoForkDrainAsync(stateForBlock, ct);
            }
        }

        private static List<Withdrawal> ToModelWithdrawals(IList<WithdrawalEntry>? withdrawals)
        {
            var result = new List<Withdrawal>(withdrawals?.Count ?? 0);
            if (withdrawals != null)
                foreach (var w in withdrawals)
                    result.Add(new Withdrawal
                    {
                        Index = w.Index,
                        ValidatorIndex = w.ValidatorIndex,
                        Address = w.Address.HexToByteArray(),
                        AmountInGwei = (ulong)w.AmountGwei
                    });
            return result;
        }

        private static byte[] PadTo32(byte[] data)
        {
            if (data == null) return new byte[32];
            if (data.Length >= 32) return data;
            var padded = new byte[32];
            Array.Copy(data, 0, padded, 32 - data.Length, data.Length);
            return padded;
        }

        /// <summary>
        /// EIP-6110 §Block validity: <i>"Beginning with the <c>FORK_BLOCK</c>, each deposit
        /// accumulated in the block MUST appear in the EIP-7685 requests list in the order they
        /// appear in the logs."</i> EIP-8282 §Request queue and system call: <i>"The execution
        /// layer prepends the contract's request-type byte and includes
        /// <c>request_type ++ request_data</c> in the block requests list, committed via the
        /// <c>requests_hash</c>"</i>.
        /// </summary>
        private static async Task<BlockExecutionRequests> CollectExecutionRequestsAsync(
            TransactionProcessor txProcessor,
            HardforkName fork,
            BlockContext blockContext,
            WitnessRecordingStateReader? witnessRecorder,
            BlockAccessListRecorder? balRecorder,
            ulong postExecutionIndex,
            List<Log> logsInBlockOrder)
        {
            var requests = BlockExecutionRequests.OpenedWithDeposits(fork, logsInBlockOrder);

            foreach (var contract in SystemCallContracts.RequestContractsFor(fork))
            {
                requests.AddFrom(contract, await txProcessor.ExecuteSystemCallAsync(
                    contract,
                    blockContext,
                    witnessRecorder,
                    balRecorder,
                    postExecutionIndex).ConfigureAwait(false));
            }

            return requests;
        }

        private async Task<(int WithdrawalsCredited, BigInteger MinerReward)> ApplyWithdrawalsAndRewardsAsync(
            BlockHeader header,
            IList<WithdrawalEntry>? withdrawals,
            IList<BlockHeader>? uncles,
            IStateStore stateForBlock,
            HardforkName fork,
            CancellationToken ct,
            BlockAccessListRecorder? balRecorder,
            ulong postExecutionIndex)
        {
            int withdrawalsCredited = 0;
            if (withdrawals != null)
            {
                balRecorder?.BeginUnit(postExecutionIndex);
                foreach (var w in withdrawals)
                {
                    balRecorder?.TouchAccount(w.Address);
                    if (balRecorder != null)
                        await TransactionProcessor.RunBalRecordingAsync(
                            () => balRecorder.PrepareAccountAsync(w.Address), "preparing (withdrawal)").ConfigureAwait(false);

                    await EthereumProofOfWorkRewardPolicy.CreditAsync(
                        stateForBlock, w.Address, w.AmountGwei * 1_000_000_000, ct).ConfigureAwait(false);
                    withdrawalsCredited++;

                    if (balRecorder != null)
                        await TransactionProcessor.RunBalRecordingAsync(
                            () => balRecorder.RecordAccountAsync(w.Address), "recording (withdrawal)").ConfigureAwait(false);
                }
            }

            var minerRewardCredited = await _rewardPolicy
                .ApplyAsync(header, uncles ?? new List<BlockHeader>(), stateForBlock, fork, ct)
                .ConfigureAwait(false);

            return (withdrawalsCredited, minerRewardCredited);
        }

        private async Task<BlockHeader?> ResolveParentHeaderAsync(BlockHeader header)
        {
            if (header == null || header.ParentHash == null || header.ParentHash.Length == 0)
                return null;
            return await _blockStore.GetByHashAsync(header.ParentHash).ConfigureAwait(false);
        }

        private static int BlobsIncluded(ISignedTransaction tx, TransactionExecutionResult result)
        {
            if (result.Skipped || !(tx is Transaction4844 blobTx) || blobTx.BlobVersionedHashes == null)
                return 0;
            return blobTx.BlobVersionedHashes.Count;
        }

        public static BlockContext BuildBlockContext(
            BlockHeader header, ChainConfig chainConfig, HardforkName fork, Func<BlockHeader, string>? authorResolver = null)
        {
            var isPostMerge = fork >= HardforkName.Paris;
            BigInteger evmDifficulty = isPostMerge && header.MixHash != null && header.MixHash.Length > 0
                ? new BigInteger(header.MixHash, isUnsigned: true, isBigEndian: true)
                : header.Difficulty;
            return new BlockContext
            {
                BlockNumber = header.BlockNumber,
                Timestamp = (long)header.Timestamp,
                Coinbase = authorResolver?.Invoke(header) ?? header.Coinbase,
                GasLimit = header.GasLimit,
                BaseFee = fork < HardforkName.London ? 0 : header.BaseFee ?? chainConfig.BaseFee,
                Difficulty = evmDifficulty,
                PrevRandao = header.MixHash,
                ChainId = chainConfig.ChainId,
                ExcessBlobGas = header.ExcessBlobGas ?? 0,
                SlotNumber = header.SlotNumber
            };
        }

        public static BlockContext BuildBlockContext(
            BlockHeader header, ChainConfig chainConfig, Func<BlockHeader, string>? authorResolver = null)
        {
            return BuildBlockContext(header, chainConfig, Nethereum.EVM.HardforkNames.Parse(chainConfig.Hardfork), authorResolver);
        }

        private static void CombineBloom(byte[] target, byte[]? source)
        {
            if (source == null || source.Length != 256) return;
            for (int i = 0; i < 256; i++) target[i] |= source[i];
        }

        private static async Task ApplyDaoForkDrainAsync(IStateStore stateStore, CancellationToken ct)
        {
            BigInteger drained = BigInteger.Zero;
            foreach (var addr in Forks.DaoForkConstants.DrainList)
            {
                var acc = await stateStore.GetAccountAsync(addr);
                if (acc == null) continue;
                var bal = new BigInteger(acc.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
                if (bal.IsZero)
                {
                    continue;
                }
                drained += bal;
                acc.Balance = EvmUInt256.Zero;
                await stateStore.SaveAccountAsync(addr, acc);
            }

            if (drained.IsZero) return;
            await EthereumProofOfWorkRewardPolicy.CreditAsync(
                stateStore, Forks.DaoForkConstants.WithdrawContractAddress, drained, ct);
        }
    }

    public readonly struct WithdrawalEntry
    {
        public WithdrawalEntry(string address, BigInteger amountGwei, ulong index, ulong validatorIndex)
        {
            Address = address;
            AmountGwei = amountGwei;
            Index = index;
            ValidatorIndex = validatorIndex;
        }

        public string Address { get; }
        public BigInteger AmountGwei { get; }
        public ulong Index { get; }
        public ulong ValidatorIndex { get; }
    }
}
