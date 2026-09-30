using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    public class BlockProducer : IBlockProducer
    {
        private static readonly byte[] EMPTY_LIST_HASH = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());

        private readonly BlockExecutor _engine;
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IStateStore _stateStore;
        private readonly IIncrementalStateRootCalculator _stateRootCalculator;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly ITransactionOrderingPolicy _orderingPolicy;
        private readonly IBlockHashProvider _blockHashProvider;
        private readonly IBlockEncodingProvider _blockEncodingProvider;
        private readonly IBlockRootsProvider _blockRootsProvider;
        private readonly IWithdrawalStore _withdrawalStore;
        private readonly IBlockAccessListStore? _blockAccessListStore;
        private readonly NodeCommitBlockContext? _nodeCommitBlockContext;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly SemaphoreSlim _produceLock = new SemaphoreSlim(1, 1);
        private readonly BlockHeaderSealer _headerSealer;

        public BlockProducer(
            BlockExecutor engine,
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            ITrieNodeStore trieNodeStore,
            IIncrementalStateRootCalculator stateRootCalculator,
            ITransactionOrderingPolicy? orderingPolicy = null,
            IBlockHashProvider? blockHashProvider = null,
            IBlockEncodingProvider? blockEncodingProvider = null,
            IBlockRootsProvider? blockRootsProvider = null,
            IWithdrawalStore? withdrawalStore = null,
            NodeCommitBlockContext? nodeCommitBlockContext = null,
            Func<HardforkName, HardforkConfig>? hardforkConfigFactory = null)
            : this(engine, blockStore, transactionStore, receiptStore, logStore, stateStore, trieNodeStore,
                   stateRootCalculator, orderingPolicy, blockHashProvider, blockEncodingProvider,
                   blockRootsProvider, withdrawalStore, nodeCommitBlockContext, blockAccessListStore: null,
                   hardforkConfigFactory: hardforkConfigFactory)
        {
        }

        public BlockProducer(
            BlockExecutor engine,
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            ITrieNodeStore trieNodeStore,
            IIncrementalStateRootCalculator stateRootCalculator,
            ITransactionOrderingPolicy? orderingPolicy,
            IBlockHashProvider? blockHashProvider,
            IBlockEncodingProvider? blockEncodingProvider,
            IBlockRootsProvider? blockRootsProvider,
            IWithdrawalStore? withdrawalStore,
            NodeCommitBlockContext? nodeCommitBlockContext,
            IBlockAccessListStore? blockAccessListStore,
            Func<HardforkName, HardforkConfig>? hardforkConfigFactory = null)
        {
            _withdrawalStore = withdrawalStore;
            _blockAccessListStore = blockAccessListStore;
            _nodeCommitBlockContext = nodeCommitBlockContext;
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trieNodeStore = trieNodeStore ?? throw new ArgumentNullException(nameof(trieNodeStore));
            _stateRootCalculator = stateRootCalculator ?? throw new ArgumentNullException(nameof(stateRootCalculator));
            _orderingPolicy = orderingPolicy ?? MempoolNonceOrderingPolicy.Instance;
            _blockHashProvider = blockHashProvider ?? RlpKeccakBlockHashProvider.Instance;
            _blockEncodingProvider = blockEncodingProvider ?? RlpBlockEncodingProvider.Instance;
            _blockRootsProvider = blockRootsProvider ?? new PatriciaBlockRootsProvider(_blockEncodingProvider, trieNodeStore: _trieNodeStore);
            _headerSealer = new BlockHeaderSealer(_blockRootsProvider);
            _hardforkConfigFactory = hardforkConfigFactory ?? Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get;
        }

        public async Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options)
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));
            if (options == null) throw new ArgumentNullException(nameof(options));

            await _produceLock.WaitAsync();
            try
            {
                return await ProduceBlockInternalAsync(transactions, options);
            }
            finally
            {
                _produceLock.Release();
            }
        }

        private async Task<BlockProductionResult> ProduceBlockInternalAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options)
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            var nextBlockNumber = latestBlock != null ? latestBlock.BlockNumber + 1 : 1;

            var ordered = OrderMempool(transactions, nextBlockNumber, options);
            var blockHeader = SynthesiseHeader(nextBlockNumber, await ResolveParentHashAsync(latestBlock), latestBlock, options);

            var journal = ArmReverseDiffJournal(nextBlockNumber);
            var preStateRoot = await ComputePreStateRootAsync(latestBlock, journal);
            var execResult = await ExecuteBlockAsync(blockHeader, ordered, options, journal, nextBlockNumber);
            RefuseFailedExecution(execResult, nextBlockNumber);
            RefuseOversizedBlockAccessList(execResult, blockHeader, nextBlockNumber);

            var body = AssembleBlockBody(execResult);
            ApplyBlobFieldsIfCarried(blockHeader, latestBlock, execResult.Fork, body.IncludedTransactions);
            var blockAccessListRlp = EncodeBlockAccessList(execResult);
            SealHeader(blockHeader, body, execResult, options, blockAccessListRlp);
            options.ApplyConsensusSeal?.Invoke(blockHeader);

            var blockHash = BlockHashCalculator.ForFork(blockHeader, execResult.Fork, _blockHashProvider);
            await PersistBlockAsync(blockHeader, blockHash, body, blockAccessListRlp, options, nextBlockNumber);

            return new BlockProductionResult
            {
                Header = blockHeader,
                BlockHash = blockHash,
                TransactionResults = body.Results,
                SuccessfulTransactions = body.SuccessCount,
                FailedTransactions = body.FailCount,
                PreStateRoot = preStateRoot,
                WitnessBytes = execResult.WitnessBytes,
                ExecutionRequests = execResult.ExecutionRequests,
                BlockAccessListRlp = blockAccessListRlp,
                IncludedTransactions = body.IncludedTransactions
            };
        }

        public async Task<BlockProductionResult> ProduceSimulatedBlockAsync(
            IReadOnlyList<TxEntry> syntheticCalls,
            BlockProductionOptions options,
            byte[] parentHash,
            BlockHeader? parentHeader,
            BigInteger blockNumber,
            SimulateCallOptions simulateCallOptions,
            EvmUInt256? baseFeeOverride = null,
            long? excessBlobGasOverride = null,
            BigInteger simulateGlobalGasUsedBefore = default)
        {
            if (syntheticCalls == null) throw new ArgumentNullException(nameof(syntheticCalls));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (simulateCallOptions == null) throw new ArgumentNullException(nameof(simulateCallOptions));

            var blockHeader = SynthesiseHeader(
                blockNumber, parentHash, parentHeader, options, baseFeeOverride);

            if (excessBlobGasOverride.HasValue)
                blockHeader.ExcessBlobGas = excessBlobGasOverride.Value;

            var execResult = await _engine.ExecuteAsync(
                blockHeader,
                syntheticCalls,
                uncles: new List<BlockHeader>(),
                withdrawals: WithdrawalAdapter.Convert(options.Withdrawals),
                new BlockExecutionOptions
                {
                    Role = BlockExecutionRole.Simulating,
                    SimulateCallOptions = simulateCallOptions,
                    SimulateGlobalGasUsedBefore = simulateGlobalGasUsedBefore,
                    ParentBeaconBlockRoot = (options.ParentBeaconBlockRoot ?? new byte[32])
                });

            RefuseFailedExecution(execResult, blockNumber);
            RefuseOversizedBlockAccessList(execResult, blockHeader, blockNumber);

            var body = AssembleBlockBody(execResult);
            var blockAccessListRlp = EncodeBlockAccessList(execResult);

            if (excessBlobGasOverride.HasValue)
                blockHeader.BlobGasUsed = TotalBlobGasUsed(body.IncludedTransactions);

            SealHeader(blockHeader, body, execResult, options, blockAccessListRlp);

            var blockHash = BlockHashCalculator.ForFork(blockHeader, execResult.Fork, _blockHashProvider);

            return new BlockProductionResult
            {
                Header = blockHeader,
                BlockHash = blockHash,
                TransactionResults = PerCallResults(execResult),
                SuccessfulTransactions = body.SuccessCount,
                FailedTransactions = body.FailCount,
                PreStateRoot = execResult.PreStateRoot,
                ExecutionRequests = execResult.ExecutionRequests,
                BlockAccessListRlp = blockAccessListRlp,
                IncludedTransactions = body.IncludedTransactions
            };
        }

        /// <summary>
        /// EIP-4844 §Header extension: "The current header encoding is extended with two new
        /// 64-bit unsigned integer fields: <c>blob_gas_used</c> ... <c>excess_blob_gas</c>."
        /// </summary>
        private void ApplyBlobFieldsIfCarried(
            BlockHeader blockHeader, BlockHeader? parentHeader, HardforkName fork,
            IReadOnlyList<ISignedTransaction> includedTransactions)
        {
            if (!Model.Codecs.BlockHeaderCodecs.ForFork(fork).CarriesBlobFieldsAndBeaconRoot) return;

            var hardforkConfig = _hardforkConfigFactory(fork);
            blockHeader.ExcessBlobGas = unchecked((long)BlockExecutor.ExpectedExcessBlobGas(parentHeader, hardforkConfig));
            blockHeader.BlobGasUsed = TotalBlobGasUsed(includedTransactions);
        }

        /// <summary>EIP-4844 <c>get_total_blob_gas</c> summed over every blob transaction the block actually included.</summary>
        private static long TotalBlobGasUsed(IReadOnlyList<ISignedTransaction> includedTransactions)
        {
            var blobCount = 0;
            foreach (var tx in includedTransactions)
                if (tx is Transaction4844 blobTx)
                    blobCount += blobTx.BlobVersionedHashes?.Count ?? 0;
            return (long)BlobGasCalculator.CalculateTotalBlobGas(blobCount);
        }

        private static List<TransactionResult> PerCallResults(BlockExecutionResult execResult)
        {
            var results = new List<TransactionResult>(execResult.Receipts.Count);
            foreach (var executed in execResult.Receipts)
            {
                results.Add(new TransactionResult
                {
                    TxHash = executed.TransactionHash,
                    Sender = executed.Sender,
                    Success = executed.Success,
                    IsRevert = executed.IsRevert,
                    IsOutOfGas = executed.IsOutOfGas,
                    Receipt = executed.Receipt,
                    Logs = executed.Logs,
                    ErrorMessage = executed.RevertReason,
                    GasUsed = executed.GasUsed,
                    GasRefund = executed.GasRefund,
                    ReturnData = executed.ReturnData
                });
            }
            return results;
        }

        private IReadOnlyList<TxEntry> OrderMempool(
            IReadOnlyList<ISignedTransaction> transactions, BigInteger blockNumber, BlockProductionOptions options)
            => _orderingPolicy.Order(
                transactions, CreateBlockContext(blockNumber, options), options.BlockGasLimit, default);

        private async Task<byte[]> ResolveParentHashAsync(BlockHeader latestBlock)
            => latestBlock != null
                ? (await _blockStore.GetHashByNumberAsync(latestBlock.BlockNumber) ?? new byte[32])
                : new byte[32];

        private static BlockHeader SynthesiseHeader(
            BigInteger blockNumber, byte[] parentHash, BlockHeader? parentHeader, BlockProductionOptions options,
            EvmUInt256? baseFeeOverride = null)
            => new BlockHeader
            {
                ParentHash = parentHash,
                UnclesHash = EMPTY_LIST_HASH,
                Coinbase = options.Coinbase,
                StateRoot = null,
                TransactionsHash = null,
                ReceiptHash = null,
                LogsBloom = new byte[256],
                Difficulty = options.Difficulty,
                BlockNumber = blockNumber,
                GasLimit = (long)options.BlockGasLimit,
                GasUsed = 0,
                Timestamp = options.Timestamp,
                ExtraData = options.ExtraData ?? Array.Empty<byte>(),
                MixHash = options.PrevRandao ?? new byte[32],
                Nonce = options.Nonce ?? new byte[8],
                BaseFee = baseFeeOverride ?? ExpectedBaseFeeFor(parentHeader),
                ParentBeaconBlockRoot = (options.ParentBeaconBlockRoot ?? new byte[32]),
                SlotNumber = options.SlotNumber
            };

        /// <summary>EIP-1559 §validate_block: "check if the base fee is correct".</summary>
        private static EvmUInt256 ExpectedBaseFeeFor(BlockHeader? parentHeader) =>
            Model.BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                parentHeader?.BaseFee, parentHeader?.GasLimit ?? 0, parentHeader?.GasUsed ?? 0);

        private IHistoricalStateProvider ArmReverseDiffJournal(BigInteger blockNumber)
        {
            var journal = _stateStore as IHistoricalStateProvider;
            journal?.SetCurrentBlockNumber(blockNumber);
            return journal;
        }

        private async Task<byte[]> ComputePreStateRootAsync(BlockHeader latestBlock, IHistoricalStateProvider journal)
        {
            try
            {
                return latestBlock?.StateRoot != null && latestBlock.StateRoot.Length > 0
                    ? await _stateRootCalculator.ComputeStateRootAsync(latestBlock.StateRoot)
                    : await _stateRootCalculator.ComputeStateRootAsync();
            }
            catch
            {
                if (journal != null)
                {
                    await journal.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
                throw;
            }
        }

        private async Task<BlockExecutionResult> ExecuteBlockAsync(
            BlockHeader blockHeader,
            IReadOnlyList<TxEntry> ordered,
            BlockProductionOptions options,
            IHistoricalStateProvider journal,
            BigInteger blockNumber)
        {
            _nodeCommitBlockContext?.Arm((ulong)blockNumber);
            try
            {
                return await _engine.ExecuteAsync(
                    blockHeader,
                    ordered,
                    uncles: new List<BlockHeader>(),
                    withdrawals: WithdrawalAdapter.Convert(options.Withdrawals),
                    new BlockExecutionOptions
                    {
                        CaptureWitness = options.CaptureWitness,
                        ParentBeaconBlockRoot = (options.ParentBeaconBlockRoot ?? new byte[32])
                    });
            }
            finally
            {
                _nodeCommitBlockContext?.Clear();
                if (journal != null)
                {
                    await journal.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
            }
        }

        private static void RefuseFailedExecution(BlockExecutionResult execResult, BigInteger blockNumber)
        {
            if (execResult.Exception == null) return;

            if (execResult.Exception is Rpc.RpcException) throw execResult.Exception;

            throw new InvalidOperationException(
                $"BlockExecutor failed at synthesised block {blockNumber}: {execResult.ErrorMessage}",
                execResult.Exception);
        }

        private static void RefuseOversizedBlockAccessList(
            BlockExecutionResult execResult, BlockHeader blockHeader, BigInteger blockNumber)
        {
            if (!execResult.BlockAccessListGasLimitExceeded) return;

            throw new InvalidOperationException(
                $"Block access list exceeds the block gas limit at synthesised block {blockNumber}: "
                + $"{BlockAccessListSizeRule.CountItems(execResult.BlockAccessList)} items against gasLimit "
                + $"{blockHeader.GasLimit}.");
        }

        private void SealHeader(
            BlockHeader header,
            AssembledBlockBody body,
            BlockExecutionResult execResult,
            BlockProductionOptions options,
            byte[]? blockAccessListRlp)
        {
            _headerSealer.SealHeader(
                header, body.IncludedTransactions, body.Receipts, body.CombinedBloom, execResult,
                options.Withdrawals, blockAccessListRlp);
        }

        private async Task PersistBlockAsync(
            BlockHeader header,
            byte[] blockHash,
            AssembledBlockBody body,
            byte[]? blockAccessListRlp,
            BlockProductionOptions options,
            BigInteger blockNumber)
        {
            _trieNodeStore?.Flush();

            await _blockStore.SaveAsync(header, blockHash);

            await PersistWithdrawalsAsync(header, blockHash, options);

            await PersistBlockAccessListAsync(blockHash, blockAccessListRlp);

            await PersistTransactionArtifactsAsync(body, blockHash, blockNumber);

            await _logStore.SaveBlockBloomAsync(blockNumber, body.CombinedBloom);
        }

        private Task PersistWithdrawalsAsync(BlockHeader header, byte[] blockHash, BlockProductionOptions options)
        {
            if (_withdrawalStore == null || header.WithdrawalsRoot == null)
                return Task.CompletedTask;

            return _withdrawalStore.SaveAsync(blockHash, options.Withdrawals ?? new List<Withdrawal>());
        }

        private static byte[]? EncodeBlockAccessList(BlockExecutionResult execResult)
            => execResult.BlockAccessList != null
                ? BlockAccessListRLPEncoder.Current.Encode(execResult.BlockAccessList)
                : null;

        private Task PersistBlockAccessListAsync(byte[] blockHash, byte[]? blockAccessListRlp)
        {
            if (_blockAccessListStore == null || blockAccessListRlp == null) return Task.CompletedTask;
            return _blockAccessListStore.SaveAsync(blockHash, blockAccessListRlp);
        }

        private sealed class AssembledBlockBody
        {
            public AssembledBlockBody(int capacity, byte[] combinedBloom)
            {
                IncludedTransactions = new List<ISignedTransaction>(capacity);
                Receipts = new List<Receipt>(capacity);
                Results = new List<TransactionResult>(capacity);
                IncludedExecResults = new List<TransactionExecutionResult>(capacity);
                CombinedBloom = combinedBloom;
            }

            public List<ISignedTransaction> IncludedTransactions { get; }
            public List<Receipt> Receipts { get; }
            public List<TransactionResult> Results { get; }
            public List<TransactionExecutionResult> IncludedExecResults { get; }
            public byte[] CombinedBloom { get; }
            public int SuccessCount { get; private set; }
            public int FailCount { get; private set; }

            public void Include(TransactionExecutionResult executed)
            {
                IncludedTransactions.Add(executed.Transaction);
                IncludedExecResults.Add(executed);
                if (executed.Receipt != null) Receipts.Add(executed.Receipt);
                Results.Add(ReportOf(executed));

                if (executed.Success) SuccessCount++; else FailCount++;
            }

            private static TransactionResult ReportOf(TransactionExecutionResult executed)
                => new TransactionResult
                {
                    TxHash = executed.TransactionHash,
                    Success = executed.Success,
                    Receipt = executed.Receipt,
                    ErrorMessage = executed.RevertReason,
                    GasUsed = executed.GasUsed,
                    GasRefund = executed.GasRefund,
                    ReturnData = executed.ReturnData
                };
        }

        private static AssembledBlockBody AssembleBlockBody(BlockExecutionResult execResult)
        {
            var body = new AssembledBlockBody(
                execResult.Receipts.Count, execResult.BlockBloom ?? new byte[256]);

            foreach (var executed in execResult.Receipts)
                if (!executed.Skipped) body.Include(executed);

            return body;
        }

        private async Task PersistTransactionArtifactsAsync(
            AssembledBlockBody body, byte[] blockHash, BigInteger nextBlockNumber)
        {
            for (int i = 0; i < body.Results.Count; i++)
            {
                var result = body.Results[i];
                var tx = body.IncludedTransactions[i];
                var execR = body.IncludedExecResults[i];

                await _transactionStore.SaveAsync(tx, blockHash, i, nextBlockNumber);

                if (result.Receipt != null)
                {
                    await _receiptStore.SaveAsync(
                        result.Receipt,
                        result.TxHash,
                        blockHash,
                        nextBlockNumber,
                        i,
                        execR.GasUsed,
                        execR.ContractAddress,
                        execR.EffectiveGasPrice);
                }

                if (execR.Logs != null && execR.Logs.Count > 0)
                {
                    await _logStore.SaveLogsAsync(
                        execR.Logs,
                        result.TxHash,
                        blockHash,
                        nextBlockNumber,
                        i);
                }
            }
        }

        private BlockContext CreateBlockContext(BigInteger blockNumber, BlockProductionOptions options)
        {
            var difficulty = options.Difficulty;
            if (options.PrevRandao != null && options.PrevRandao.Length > 0)
            {
                difficulty = new BigInteger(options.PrevRandao, isUnsigned: true, isBigEndian: true);
            }

            return new BlockContext
            {
                BlockNumber = blockNumber,
                Timestamp = options.Timestamp,
                GasLimit = options.BlockGasLimit,
                BaseFee = options.BaseFee,
                Coinbase = options.Coinbase,
                ChainId = options.ChainId,
                Difficulty = difficulty,
                PrevRandao = options.PrevRandao,
                SlotNumber = options.SlotNumber
            };
        }
    }
}
