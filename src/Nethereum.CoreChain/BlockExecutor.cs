using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
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
        private readonly Func<HardforkName, ChainConfig> _chainConfigFactory;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly IIncrementalStateRootCalculator _stateRootCalculator;
        private readonly IRewardPolicy _rewardPolicy;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly ILogger<BlockExecutor>? _logger;
        private readonly Nethereum.Model.IBlockRootsProvider _blockRootsProvider;
        private readonly Func<BlockHeader, string>? _authorResolver;

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

            ulong blockNumber = (ulong)header.BlockNumber;
            ulong timestamp = (ulong)header.Timestamp;
            var fork = _activations.ResolveAt((long)blockNumber, timestamp);

            var carriesBlockAccessList = HeaderCarriesBlockAccessList(fork);
            var balRecorder = carriesBlockAccessList ? new BlockAccessListRecorder(stateForBlock) : null;

            if (validating)
            {
                var blobFieldFormatRefusal = RefuseHeaderWithIncorrectBlobFieldFormat(header, fork);
                if (blobFieldFormatRefusal != null) return blobFieldFormatRefusal;
            }

            var gasCapacityRefusal = RefuseHeaderThatDeclaresMoreGasThanItAllows(header, fork);
            if (gasCapacityRefusal != null) return gasCapacityRefusal;

            var maxBlobsPerBlock = _hardforkConfigFactory(fork).MaxBlobsPerBlock;
            var blobGasCapacityRefusal =
                RefuseHeaderThatDeclaresMoreBlobGasThanTheForkAllows(header, fork, maxBlobsPerBlock);
            if (blobGasCapacityRefusal != null) return blobGasCapacityRefusal;

            BlockHeader? parentHeader = await ResolveParentHeaderAsync(header).ConfigureAwait(false);
            byte[]? preStateRoot = parentHeader?.StateRoot;

            var gasLimitRefusal = RefuseHeaderWithInvalidGasLimit(header, fork);
            if (gasLimitRefusal != null) return gasLimitRefusal;

            if (parentHeader != null && !simulating)
            {
                if (validating)
                {
                    var parentBoundRefusal = RefuseHeaderOutsideParentGasLimitBound(header, parentHeader, fork);
                    if (parentBoundRefusal != null) return parentBoundRefusal;
                }

                if (HeaderCarriesBaseFee(fork))
                {
                    var baseFeeRefusal = RefuseHeaderWithIncorrectBaseFee(header, parentHeader, fork);
                    if (baseFeeRefusal != null) return baseFeeRefusal;
                }

                if (HeaderCarriesBlobFields(fork))
                {
                    var excessBlobGasRefusal =
                        RefuseHeaderWithIncorrectExcessBlobGas(header, parentHeader, fork, _hardforkConfigFactory(fork));
                    if (excessBlobGasRefusal != null) return excessBlobGasRefusal;
                }
            }

            var accumulated = new BlockAccumulation(txs.Count);
            var outcome = new BlockExecutionAccumulator();
            WitnessRecordingStateReader? witnessRecorder = null;

            try
            {
                witnessRecorder = ArmWitnessRecorder(options, stateForBlock);

                var chainConfig = _chainConfigFactory(fork);
                var hardforkConfig = _hardforkConfigFactory(fork);
                var txVerifier = new Nethereum.Signer.TransactionVerificationAndRecoveryImp();
                var txProcessor = new TransactionProcessor(stateForBlock, _blockStore, chainConfig, txVerifier, hardforkConfig,
                    eip158EmptyAccountPruning: fork >= HardforkName.SpuriousDragon);
                var blockContext = BuildBlockContext(header, chainConfig, fork, _authorResolver);

                await ApplyPreTransactionSystemCallsAsync(
                    stateForBlock, txProcessor, blockContext, header, fork, blockNumber, options, witnessRecorder, balRecorder, ct).ConfigureAwait(false);

                if (CanWarmStartFromParentRoot(options, preStateRoot))
                {
                    await WarmStartStateRootCalculatorFromParentAsync(preStateRoot).ConfigureAwait(false);
                }

                IStateReader? stateReaderForExecution = witnessRecorder;

                var cost = ExecutionCostBaseline.CaptureAndStartExecutionTimer(_stateStore);
                for (int i = 0; i < txs.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = txs[i];
                    if (simulating)
                    {
                        var runningNonce = await ResolveSimulateRunningNonceAsync(stateForBlock, entry).ConfigureAwait(false);
                        entry = ResolveSimulateEntry(
                            entry, chainConfig.RpcGasCap, header.GasLimit,
                            (long)accumulated.CumulativeGasUsed, options.SimulateGlobalGasUsedBefore,
                            blockContext.ChainId, runningNonce);
                    }
                    var txResult = await ExecuteTransactionAtIndexAsync(
                        txProcessor, entry, blockContext, i, accumulated.CumulativeGasUsed,
                        stateReaderForExecution, options, balRecorder, accumulated.Capacity).ConfigureAwait(false);

                    await StampIntermediatePostStateRootWhenRequiredAsync(
                        txResult, hardforkConfig, options).ConfigureAwait(false);

                    accumulated.Add(entry.Tx, txResult, validating,
                        hardforkConfig.IntrinsicGasRules.StateGasActive);
                }

                outcome.BlockBloom = accumulated.BlockBloom;

                var postExecutionIndex = (ulong)(txs.Count + 1);

                var executionRequests = await CollectExecutionRequestsAsync(
                    txProcessor, fork, blockContext, witnessRecorder, balRecorder, postExecutionIndex,
                    accumulated.Logs).ConfigureAwait(false);
                outcome.ComputedRequestsHash = executionRequests.Commitment();
                outcome.ExecutionRequests = executionRequests.NonEmptyRequests();

                (outcome.WithdrawalsCredited, outcome.MinerRewardCredited) = await ApplyWithdrawalsAndRewardsAsync(
                    header, withdrawals, uncles, stateForBlock, fork, ct, balRecorder, postExecutionIndex).ConfigureAwait(false);

                if (balRecorder != null)
                {
                    (outcome.BlockAccessList, outcome.BlockAccessListGasLimitExceeded) =
                        BuildBlockAccessListAndApplyItsSizeRule(balRecorder, header);
                }

                outcome.BlockAccessListMalformed = DeclaredBlockAccessListIsMalformed(options, txs.Count);

                if (!options.ReadOnly || simulating)
                {
                    outcome.Settlement = await SettlePostStateAndDecideCommitAsync(
                        header, accumulated.Receipts, outcome.BlockBloom, accumulated.Capacity, outcome.BlockAccessList, carriesBlockAccessList,
                        validating, simulating, accumulated.ContainsInvalidTransaction, outcome.BlockAccessListGasLimitExceeded,
                        txs.Count, accumulated.CumulativeGasUsed, cost)
                        .ConfigureAwait(false);
                }

                outcome.WitnessBytes = BuildBlockWitness(options, witnessRecorder, txs, header, chainConfig, fork,
                    withdrawals, options.ParentBeaconBlockRoot ?? header.ParentBeaconBlockRoot, preStateRoot);
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

            return BuildExecutionResult(outcome, accumulated, header, fork, preStateRoot, withdrawals, validating);
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
                RequestsHashMismatch = RequestsHashDoesNotMatchTheHeader(header, outcome.ComputedRequestsHash),
                WithdrawalsRootMismatch = WithdrawalsRootDoesNotMatchTheHeader(header, withdrawals, validating),
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
                BlockAccessListGasLimitExceeded = outcome.BlockAccessListGasLimitExceeded,
                BlockAccessListMalformed = outcome.BlockAccessListMalformed,
                ContainsInvalidTransaction = accumulated.ContainsInvalidTransaction,
                BlockAccessList = outcome.BlockAccessList,
                Exception = outcome.Exception,
                ErrorMessage = outcome.ErrorMessage
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

        private IStateStore SelectStateStoreForBlock(BlockExecutionOptions options) =>
            options.Role == BlockExecutionRole.Simulating
                ? _stateStore
                : options.ReadOnly
                    ? new StateLayer().Stores.WitnessCapture(_stateStore)
                    : _stateStore;

        private static bool HeaderCarriesBlockAccessList(HardforkName fork) =>
            Model.Codecs.BlockHeaderCodecs.ForFork(fork).CarriesBlockAccessList;

        private static bool HeaderCarriesBlobFields(HardforkName fork) =>
            Model.Codecs.BlockHeaderCodecs.ForFork(fork).CarriesBlobFieldsAndBeaconRoot;

        private static bool HeaderCarriesBaseFee(HardforkName fork) =>
            Model.Codecs.BlockHeaderCodecs.ForFork(fork).CarriesBaseFee;

        private const long GasLimitMinimum = 5000;

        private const long GasLimitBoundDivisor = 1024;

        private const long ElasticityMultiplier = 2;

        private static BlockExecutionResult? RefuseHeaderWithInvalidGasLimit(
            BlockHeader header, HardforkName fork)
            => header.GasLimit < GasLimitMinimum ? GasLimitBoundRefusal(fork) : null;

        private static BlockExecutionResult? RefuseHeaderOutsideParentGasLimitBound(
            BlockHeader header, BlockHeader parent, HardforkName fork)
        {
            var parentGasLimit = new System.Numerics.BigInteger(unchecked((ulong)parent.GasLimit));
            if (parentGasLimit.IsZero) return null;

            if (parent.BaseFee == null && header.BaseFee != null)
                parentGasLimit *= ElasticityMultiplier;

            var headerGasLimit = new System.Numerics.BigInteger(unchecked((ulong)header.GasLimit));
            var difference = System.Numerics.BigInteger.Abs(headerGasLimit - parentGasLimit);
            return difference < parentGasLimit / GasLimitBoundDivisor ? null : GasLimitBoundRefusal(fork);
        }

        private static BlockExecutionResult GasLimitBoundRefusal(HardforkName fork) => new BlockExecutionResult
        {
            Fork = fork,
            PreStateRoot = null,
            PostStateRoot = null,
            Receipts = Array.Empty<TransactionExecutionResult>(),
            Logs = Array.Empty<Log>(),
            GasLimitBoundViolated = true,
            GasUsed = 0
        };

        private static BlockExecutionResult? RefuseHeaderThatDeclaresMoreGasThanItAllows(
            BlockHeader header, HardforkName fork)
        {
            if (header.GasUsed > header.GasLimit)
            {
                return new BlockExecutionResult
                {
                    Fork = fork,
                    PreStateRoot = null,
                    PostStateRoot = null,
                    Receipts = Array.Empty<TransactionExecutionResult>(),
                    Logs = Array.Empty<Log>(),
                    GasCapacityExceeded = true,
                    GasUsed = 0
                };
            }
            return null;
        }

        private static BlockExecutionResult? RefuseHeaderThatDeclaresMoreBlobGasThanTheForkAllows(
            BlockHeader header, HardforkName fork, int maxBlobsPerBlock)
        {
            if (header.BlobGasUsed is long declaredBlobGasUsed
                && declaredBlobGasUsed > (long)maxBlobsPerBlock * BlobGasCalculator.GAS_PER_BLOB)
            {
                return new BlockExecutionResult
                {
                    Fork = fork,
                    PreStateRoot = null,
                    PostStateRoot = null,
                    Receipts = Array.Empty<TransactionExecutionResult>(),
                    Logs = Array.Empty<Log>(),
                    BlobGasCapacityExceeded = true,
                    GasUsed = 0
                };
            }
            return null;
        }

        /// <summary>
        /// EIP-4844 §Header extension: "The current header encoding is extended with two new
        /// 64-bit unsigned integer fields: `blob_gas_used` ... `excess_blob_gas`."
        /// </summary>
        private static BlockExecutionResult? RefuseHeaderWithIncorrectBlobFieldFormat(
            BlockHeader header, HardforkName fork)
        {
            var expectsBlobFields = HeaderCarriesBlobFields(fork);
            var blobGasUsedMatchesFormat = header.BlobGasUsed.HasValue == expectsBlobFields;
            var excessBlobGasMatchesFormat = header.ExcessBlobGas.HasValue == expectsBlobFields;

            if (blobGasUsedMatchesFormat && excessBlobGasMatchesFormat) return null;

            return new BlockExecutionResult
            {
                Fork = fork,
                PreStateRoot = null,
                PostStateRoot = null,
                Receipts = Array.Empty<TransactionExecutionResult>(),
                Logs = Array.Empty<Log>(),
                BlobFieldFormatMismatch = true,
                GasUsed = 0
            };
        }

        /// <summary>
        /// EIP-4844: "each block's <c>header.excess_blob_gas</c> ... must be validated ...
        /// Clients should reject blocks where this field ... is incorrect with respect to
        /// the parent block" - calc_excess_blob_gas(parent) per
        /// <see cref="BlobGasCalculator.CalculateExcessBlobGas"/>. A parent that predates the
        /// blob fork (no declared excess/used) contributes 0 to the recurrence, matching the
        /// genesis-of-the-blob-fork case.
        /// </summary>
        private static BlockExecutionResult? RefuseHeaderWithIncorrectExcessBlobGas(
            BlockHeader header, BlockHeader? parentHeader, HardforkName fork, HardforkConfig hardforkConfig)
        {
            ulong expectedExcessBlobGas = ExpectedExcessBlobGas(parentHeader, hardforkConfig);
            ulong declaredExcessBlobGas = header.ExcessBlobGas is long declared ? unchecked((ulong)declared) : 0UL;

            if (declaredExcessBlobGas != expectedExcessBlobGas)
            {
                return new BlockExecutionResult
                {
                    Fork = fork,
                    PreStateRoot = null,
                    PostStateRoot = null,
                    Receipts = Array.Empty<TransactionExecutionResult>(),
                    Logs = Array.Empty<Log>(),
                    ExcessBlobGasMismatch = true,
                    GasUsed = 0
                };
            }
            return null;
        }

        internal static ulong ExpectedExcessBlobGas(BlockHeader? parentHeader, HardforkConfig hardforkConfig)
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

        /// <summary>
        /// EIP-1559 §validate_block: "check if the base fee is correct" -
        /// <c>expected_base_fee_per_gas</c> is computed from the parent per
        /// <see cref="Model.BaseFeeCalculator.CalculateExpectedBaseFeePerGas"/> and the block
        /// is invalid if it declares anything else. A parent that predates London (no
        /// declared base fee) contributes INITIAL_BASE_FEE to the calculation, matching the
        /// EIP's own fork-block case.
        /// </summary>
        private static BlockExecutionResult? RefuseHeaderWithIncorrectBaseFee(
            BlockHeader header, BlockHeader? parentHeader, HardforkName fork)
        {
            var expectedBaseFee = Model.BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                parentHeader?.BaseFee, parentHeader?.GasLimit ?? 0, parentHeader?.GasUsed ?? 0);

            if (header.BaseFee is not EvmUInt256 declaredBaseFee || declaredBaseFee != expectedBaseFee)
            {
                return new BlockExecutionResult
                {
                    Fork = fork,
                    PreStateRoot = null,
                    PostStateRoot = null,
                    Receipts = Array.Empty<TransactionExecutionResult>(),
                    Logs = Array.Empty<Log>(),
                    BaseFeeMismatch = true,
                    GasUsed = 0
                };
            }
            return null;
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

        private static async Task<EvmUInt256> ResolveSimulateRunningNonceAsync(IStateStore stateForBlock, TxEntry entry)
        {
            if (entry.SimulateCall == null || entry.SimulateCall.Nonce != null)
                return EvmUInt256.Zero;

            var account = await stateForBlock.GetAccountAsync(entry.CachedSender ?? AddressUtil.ZERO_ADDRESS).ConfigureAwait(false);
            return account?.Nonce ?? EvmUInt256.Zero;
        }

        private static TxEntry ResolveSimulateEntry(
            TxEntry entry, BigInteger rpcGasCap, long blockGasLimit, long blockGasUsedSoFar,
            BigInteger globalGasUsedBefore, BigInteger chainId, EvmUInt256 runningNonce)
        {
            if (entry.SimulateCall == null)
                return entry;

            var needsGas = entry.SimulateCall.Gas == null;
            var needsNonce = entry.SimulateCall.Nonce == null;
            if (!needsGas && !needsNonce)
                return entry;

            var blockRemaining = blockGasLimit - blockGasUsedSoFar;
            var pooledGas = rpcGasCap > 0
                ? BigInteger.Min(blockRemaining, rpcGasCap - (globalGasUsedBefore + blockGasUsedSoFar))
                : blockRemaining;
            var gas = entry.SimulateCall.Gas != null
                ? entry.SimulateCall.Gas.Value
                : BigInteger.Max(0, pooledGas);
            var nonce = entry.SimulateCall.Nonce ?? new HexBigInteger((BigInteger)runningNonce);

            var resolvedCall = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = entry.SimulateCall.From,
                To = entry.SimulateCall.To,
                Data = entry.SimulateCall.Data,
                Value = entry.SimulateCall.Value,
                Gas = new HexBigInteger(gas),
                GasPrice = entry.SimulateCall.GasPrice,
                MaxFeePerGas = entry.SimulateCall.MaxFeePerGas,
                MaxPriorityFeePerGas = entry.SimulateCall.MaxPriorityFeePerGas,
                Nonce = nonce
            };

            var tx = BuildSyntheticSimulateTransaction(resolvedCall, chainId);
            return new TxEntry(tx, entry.CachedSender, resolvedCall);
        }

        internal static ISignedTransaction BuildSyntheticSimulateTransaction(Nethereum.RPC.Eth.DTOs.TransactionInput call, BigInteger chainId)
        {
            return new Transaction1559(
                chainId: (EvmUInt256)chainId,
                nonce: call.Nonce != null ? (EvmUInt256)call.Nonce.Value : EvmUInt256.Zero,
                maxPriorityFeePerGas: call.MaxPriorityFeePerGas != null ? (EvmUInt256)call.MaxPriorityFeePerGas.Value : EvmUInt256.Zero,
                maxFeePerGas: call.MaxFeePerGas != null ? (EvmUInt256)call.MaxFeePerGas.Value : EvmUInt256.Zero,
                gasLimit: call.Gas != null ? (EvmUInt256)call.Gas.Value : EvmUInt256.Zero,
                receiverAddress: call.To ?? string.Empty,
                amount: call.Value != null ? (EvmUInt256)call.Value.Value : EvmUInt256.Zero,
                data: call.Data ?? "0x",
                accessList: null);
        }

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
                bool blockAccessListHashMismatch)
            {
                PostStateRoot = postStateRoot;
                StateRootMismatch = stateRootMismatch;
                ReceiptsRootMismatch = receiptsRootMismatch;
                LogsBloomMismatch = logsBloomMismatch;
                GasUsedMismatch = gasUsedMismatch;
                BlobGasUsedMismatch = blobGasUsedMismatch;
                BlockAccessListHashMismatch = blockAccessListHashMismatch;
            }

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
            ExecutionCostBaseline cost)
        {
            cost.ExecutionTimer.Stop();
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
                var commitments = CompareHeaderCommitments(
                    header, receipts, blockBloom, capacity, blockAccessList, carriesBlockAccessList);

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
            var swPersist = System.Diagnostics.Stopwatch.StartNew();
            if (simulating || blockIsRefused)
                _stateRootCalculator.DiscardPendingState();
            else
                await _stateRootCalculator.PersistPendingStateAsync().ConfigureAwait(false);
            swPersist.Stop();
            _logger?.LogInformation(
                "block.exec.timing block={BlockNumber} txs={TxCount} gas={Gas} exec_ms={ExecMs} root_ms={RootMs} persist_ms={PersistMs} gc1={Gc1} gc2={Gc2} alloc_mb={AllocMb} acct_reads={AcctReads} slot_reads={SlotReads}",
                header.BlockNumber, txCount, (long)cumulativeGasUsed,
                cost.ExecutionTimer.ElapsedMilliseconds, swRoot.ElapsedMilliseconds, swPersist.ElapsedMilliseconds,
                cost.Gc1Collections, cost.Gc2Collections, cost.AllocatedMegabytes,
                cost.AccountReads,
                cost.StorageReads);

            return outcome;
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

        private readonly struct HeaderCommitmentComparison
        {
            public HeaderCommitmentComparison(
                bool receiptsRootMismatch,
                bool logsBloomMismatch,
                bool gasUsedMismatch,
                bool blobGasUsedMismatch,
                bool blockAccessListHashMismatch)
            {
                ReceiptsRootMismatch = receiptsRootMismatch;
                LogsBloomMismatch = logsBloomMismatch;
                GasUsedMismatch = gasUsedMismatch;
                BlobGasUsedMismatch = blobGasUsedMismatch;
                BlockAccessListHashMismatch = blockAccessListHashMismatch;
            }

            public bool ReceiptsRootMismatch { get; }
            public bool LogsBloomMismatch { get; }
            public bool GasUsedMismatch { get; }
            public bool BlobGasUsedMismatch { get; }

            public bool BlockAccessListHashMismatch { get; }
        }

        private HeaderCommitmentComparison CompareHeaderCommitments(
            BlockHeader header,
            List<TransactionExecutionResult> receipts,
            byte[]? blockBloom,
            Nethereum.EVM.Gas.BlockGasCapacity capacity,
            List<AccountChanges>? blockAccessList,
            bool carriesBlockAccessList)
        {
            var computedReceiptsRoot = _blockRootsProvider.CalculateReceiptsRoot(TrieMemberReceipts(receipts));

            var computedBlobGasUsed = (long)capacity.BlobCount * BlobGasCalculator.GAS_PER_BLOB;

            var computedBlockAccessListHash = carriesBlockAccessList && blockAccessList != null
                ? BlockAccessListRLPEncoder.Current.Hash(blockAccessList)
                : null;

            return new HeaderCommitmentComparison(
                receiptsRootMismatch: header.ReceiptHash != null
                    && !ByteUtil.AreEqual(computedReceiptsRoot, header.ReceiptHash),
                logsBloomMismatch: header.LogsBloom != null
                    && !ByteUtil.AreEqual(blockBloom ?? new byte[256], header.LogsBloom),
                gasUsedMismatch: capacity.HeaderGasUsed != header.GasUsed,
                blobGasUsedMismatch: header.BlobGasUsed is long declaredBlobGasUsed
                    && computedBlobGasUsed != declaredBlobGasUsed,
                blockAccessListHashMismatch: carriesBlockAccessList
                    && (computedBlockAccessListHash == null
                        || header.BlockAccessListHash == null
                        || !ByteUtil.AreEqual(computedBlockAccessListHash, header.BlockAccessListHash)));
        }

        private static List<Receipt> TrieMemberReceipts(List<TransactionExecutionResult> receipts)
        {
            var members = new List<Receipt>(receipts.Count);
            foreach (var executed in receipts)
            {
                if (executed.Skipped) continue;
                if (executed.Receipt != null) members.Add(executed.Receipt);
            }
            return members;
        }

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

        private bool WithdrawalsRootDoesNotMatchTheHeader(
            BlockHeader header, IList<WithdrawalEntry>? withdrawals, bool validating)
        {
            if (!validating || header.WithdrawalsRoot == null) return false;
            var computed = _blockRootsProvider.CalculateWithdrawalsRoot(ToModelWithdrawals(withdrawals));
            return !ByteUtil.AreEqual(computed, header.WithdrawalsRoot);
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

        private static bool RequestsHashDoesNotMatchTheHeader(BlockHeader header, byte[]? computed)
        {
            if (computed == null || header.RequestsHash == null) return false;
            if (header.RequestsHash.Length != computed.Length) return true;

            for (var i = 0; i < computed.Length; i++)
                if (header.RequestsHash[i] != computed[i])
                    return true;

            return false;
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

        private static byte[]? BuildBlockWitness(
            BlockExecutionOptions options,
            WitnessRecordingStateReader? witnessRecorder,
            IReadOnlyList<TxEntry> txs,
            BlockHeader header,
            ChainConfig chainConfig,
            HardforkName fork,
            IList<WithdrawalEntry>? withdrawals,
            byte[]? parentBeaconBlockRoot,
            byte[]? preStateRoot)
        {
            if (!(options.CaptureWitness && witnessRecorder != null))
                return null;

            var witnessTxs = new List<BlockWitnessTransaction>(txs.Count);
            foreach (var entry in txs)
            {
                witnessTxs.Add(new BlockWitnessTransaction
                {
                    From = entry.CachedSender ?? "",
                    RlpEncoded = entry.Tx.GetRLPEncoded(),

                    AuthorisationAuthorities = entry.Tx.RecoverAuthorities()
                });
            }

            var witnessData = new BlockWitnessData
            {
                BlockNumber = (long)header.BlockNumber,
                Timestamp = header.Timestamp,
                BaseFee = (long)(header.BaseFee ?? 0),
                BlockGasLimit = header.GasLimit,
                ChainId = (long)chainConfig.ChainId,
                Coinbase = header.Coinbase,
                Difficulty = header.MixHash ?? new byte[32],
                PreStateRoot = preStateRoot,
                ParentHash = header.ParentHash ?? new byte[32],
                ExtraData = header.ExtraData ?? Array.Empty<byte>(),
                MixHash = header.MixHash ?? new byte[32],
                Nonce = header.Nonce ?? new byte[8],
                ComputePostStateRoot = true,

                ParentBeaconBlockRoot = parentBeaconBlockRoot,
                BlobGasUsed = header.BlobGasUsed.HasValue ? (long)header.BlobGasUsed.Value : (long?)null,
                ExcessBlobGas = header.ExcessBlobGas.HasValue ? (long)header.ExcessBlobGas.Value : (long?)null,
                RequestsHash = header.RequestsHash,
                SlotNumber = header.SlotNumber,
                Withdrawals = ToWitnessWithdrawals(withdrawals),

                Features = new BlockFeatureConfig
                {
                    Fork = fork
                },
                Transactions = witnessTxs,
                Accounts = witnessRecorder.GetWitnessAccounts()
            };

            return BinaryBlockWitness.Serialize(witnessData);
        }

        private static List<BlockWithdrawal>? ToWitnessWithdrawals(IList<WithdrawalEntry>? withdrawals)
        {
            if (withdrawals == null) return null;

            var result = new List<BlockWithdrawal>(withdrawals.Count);
            foreach (var w in withdrawals)
            {
                result.Add(new BlockWithdrawal
                {
                    Index = w.Index,
                    ValidatorIndex = w.ValidatorIndex,
                    Address = w.Address,
                    AmountInGwei = (ulong)w.AmountGwei
                });
            }
            return result;
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
                BaseFee = header.BaseFee ?? chainConfig.BaseFee,
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
