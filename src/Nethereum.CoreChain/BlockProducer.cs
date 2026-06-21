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
using Nethereum.EVM.Witness;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Sequencer wrapper around <see cref="BlockExecutor"/>. Owns the
    /// sequencer-only concerns the engine deliberately doesn't touch:
    ///   - Ordering and filtering the mempool via
    ///     <see cref="ITransactionOrderingPolicy"/>.
    ///   - Synthesising the new <see cref="BlockHeader"/> (number,
    ///     timestamp, miner, parent hash, gas limit) before execution.
    ///   - Computing the canonical block roots
    ///     (TransactionsHash / ReceiptHash / WithdrawalsRoot) and the
    ///     block hash via the wired
    ///     <see cref="IBlockHashProvider"/> /
    ///     <see cref="IBlockRootsProvider"/>.
    ///   - Persisting header + tx / receipt / log bodies.
    ///   - Arming the reverse-diff journal lifecycle around the engine
    ///     call (same pattern as <see cref="BlockImporter"/>).
    ///
    /// The transaction execution loop, system calls (EIP-4788 / EIP-2935),
    /// withdrawals, and rewards all live in the engine. This wrapper is
    /// data + sequencer policy; the engine is execution.
    /// </summary>
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
        private readonly SemaphoreSlim _produceLock = new SemaphoreSlim(1, 1);

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
            IBlockRootsProvider? blockRootsProvider = null)
        {
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
        }

        public async Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options)
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Serialise block production to protect state trie from concurrent access.
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
            var blockContext = CreateBlockContext(nextBlockNumber, options);

            // Order + filter the mempool through the configured policy.
            // Ordering is sequencer-owned; the engine trusts whatever order
            // it gets.
            var ordered = _orderingPolicy.Order(transactions, blockContext, options.BlockGasLimit, default);

            var parentHash = latestBlock != null
                ? (await _blockStore.GetHashByNumberAsync(latestBlock.BlockNumber) ?? new byte[32])
                : new byte[32];

            // Synthesise the header up front so the engine sees the right
            // (number, timestamp, coinbase, parent hash, gas limit). State
            // root, tx root, receipts root, and block hash are stamped
            // after execution.
            var blockHeader = new BlockHeader
            {
                ParentHash = parentHash ?? new byte[32],
                UnclesHash = EMPTY_LIST_HASH,
                Coinbase = options.Coinbase,
                StateRoot = null,
                TransactionsHash = null,
                ReceiptHash = null,
                LogsBloom = new byte[256],
                Difficulty = options.Difficulty,
                BlockNumber = nextBlockNumber,
                GasLimit = (long)options.BlockGasLimit,
                GasUsed = 0,
                Timestamp = options.Timestamp,
                ExtraData = options.ExtraData ?? Array.Empty<byte>(),
                MixHash = options.PrevRandao ?? new byte[32],
                Nonce = options.Nonce ?? new byte[8],
                BaseFee = options.BaseFee,
                ParentBeaconBlockRoot = options.ParentBeaconBlockRoot
            };

            // Arm the journal so this block's writes record reverse-diffs
            // for rewind. Symmetric to BlockImporter — the engine does NOT
            // touch the journal; that is a persistence concern owned by
            // the wrapper.
            var historyProvider = _stateStore as IHistoricalStateProvider;
            historyProvider?.SetCurrentBlockNumber(nextBlockNumber);

            // Pre-state root is the parent block's StateRoot — the engine
            // never recomputes. When available, lazy-load the trie from it;
            // genesis path falls through.
            byte[] preStateRoot;
            try
            {
                preStateRoot = latestBlock?.StateRoot != null && latestBlock.StateRoot.Length > 0
                    ? await _stateRootCalculator.ComputeStateRootAsync(latestBlock.StateRoot)
                    : await _stateRootCalculator.ComputeStateRootAsync();
            }
            catch
            {
                if (historyProvider != null)
                {
                    await historyProvider.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
                throw;
            }

            BlockExecutionResult execResult;
            try
            {
                execResult = await _engine.ExecuteAsync(
                    blockHeader,
                    ordered,
                    uncles: new List<BlockHeader>(),
                    withdrawals: null,
                    new BlockExecutionOptions
                    {
                        CaptureWitness = options.CaptureWitness,
                        ParentBeaconBlockRoot = options.ParentBeaconBlockRoot
                    });
            }
            finally
            {
                if (historyProvider != null)
                {
                    await historyProvider.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
            }

            if (execResult.Exception != null)
            {
                throw new InvalidOperationException(
                    $"BlockExecutor failed at synthesised block {nextBlockNumber}: {execResult.ErrorMessage}",
                    execResult.Exception);
            }

            // Filter out skipped txs (nonce mismatch etc) when building the
            // tx body and receipts.
            var includedTransactions = new List<ISignedTransaction>(execResult.Receipts.Count);
            var receipts = new List<Receipt>(execResult.Receipts.Count);
            var results = new List<TransactionResult>(execResult.Receipts.Count);
            var includedExecResults = new List<TransactionExecutionResult>(execResult.Receipts.Count);
            long cumulativeGasUsed = 0;
            int successCount = 0;
            int failCount = 0;
            var combinedBloom = execResult.BlockBloom ?? new byte[256];

            for (int i = 0; i < execResult.Receipts.Count; i++)
            {
                var er = execResult.Receipts[i];
                if (er.Skipped) continue;

                includedTransactions.Add(er.Transaction);
                includedExecResults.Add(er);
                if (er.Receipt != null) receipts.Add(er.Receipt);

                results.Add(new TransactionResult
                {
                    TxHash = er.TransactionHash,
                    Success = er.Success,
                    Receipt = er.Receipt,
                    ErrorMessage = er.RevertReason,
                    GasUsed = er.GasUsed,
                    ReturnData = er.ReturnData
                });

                cumulativeGasUsed = (long)er.CumulativeGasUsed;
                if (er.Success) successCount++; else failCount++;
            }

            var transactionsRoot = _blockRootsProvider.CalculateTransactionsRoot(includedTransactions);
            var receiptsRoot = _blockRootsProvider.CalculateReceiptsRoot(receipts);

            // Stamp the computed roots + gas used + state root onto the
            // header before computing the block hash.
            blockHeader.StateRoot = execResult.PostStateRoot;
            blockHeader.TransactionsHash = transactionsRoot;
            blockHeader.ReceiptHash = receiptsRoot;
            blockHeader.LogsBloom = combinedBloom;
            blockHeader.GasUsed = cumulativeGasUsed;
            _trieNodeStore?.Flush();

            var blockHash = CalculateBlockHash(blockHeader);
            await _blockStore.SaveAsync(blockHeader, blockHash);

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var tx = includedTransactions[i];
                var execR = includedExecResults[i];

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

            await _logStore.SaveBlockBloomAsync(nextBlockNumber, combinedBloom);

            return new BlockProductionResult
            {
                Header = blockHeader,
                BlockHash = blockHash,
                TransactionResults = results,
                SuccessfulTransactions = successCount,
                FailedTransactions = failCount,
                PreStateRoot = preStateRoot,
                WitnessBytes = execResult.WitnessBytes
            };
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
                PrevRandao = options.PrevRandao
            };
        }

        private byte[] CalculateBlockHash(BlockHeader header)
            => _blockHashProvider.ComputeBlockHash(header);
    }
}
