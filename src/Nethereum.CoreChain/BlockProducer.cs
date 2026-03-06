using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class BlockProducer : IBlockProducer
    {
        private static readonly byte[] EMPTY_LIST_HASH = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());

        private const string BEACON_ROOTS_ADDRESS = "0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02";
        private const int HISTORY_BUFFER_LENGTH = 8191;

        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IStateStore _stateStore;
        private readonly TransactionProcessor _transactionProcessor;
        private readonly RootCalculator _rootCalculator;
        private readonly IncrementalStateRootCalculator _stateRootCalculator;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly Sha3Keccack _keccak = new();
        private readonly SemaphoreSlim _produceLock = new SemaphoreSlim(1, 1);

        public BlockProducer(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            TransactionProcessor transactionProcessor,
            ITrieNodeStore trieNodeStore = null,
            IncrementalStateRootCalculator stateRootCalculator = null)
        {
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _transactionProcessor = transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor));
            _trieNodeStore = trieNodeStore;
            _rootCalculator = new RootCalculator();
            _stateRootCalculator = stateRootCalculator ?? new IncrementalStateRootCalculator(stateStore, trieNodeStore);
        }

        public async Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options)
        {
            // Serialize block production to protect state trie from concurrent access
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

            // EIP-4788: Execute beacon root system call before processing transactions
            if (options.ParentBeaconBlockRoot != null && options.ParentBeaconBlockRoot.Length == 32)
            {
                await ExecuteBeaconRootSystemCallAsync(options.Timestamp, options.ParentBeaconBlockRoot);
            }

            if (_stateStore is IHistoricalStateProvider historyProvider)
                historyProvider.SetCurrentBlockNumber(nextBlockNumber);

            var (orderedTransactions, senderCache) = OrderTransactionsByNonce(transactions);

            var includedTransactions = new List<ISignedTransaction>();
            var results = new List<TransactionResult>();
            var execResults = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedTransactions = new List<byte[]>();
            BigInteger cumulativeGasUsed = 0;
            var combinedBloom = new byte[256];
            int successCount = 0;
            int failCount = 0;

            int txIndexInBlock = 0;
            for (int i = 0; i < orderedTransactions.Count; i++)
            {
                var tx = orderedTransactions[i];

                var txGasLimit = tx.GetGasLimit();
                if (cumulativeGasUsed + txGasLimit > options.BlockGasLimit)
                    continue;

                senderCache.TryGetValue(i, out var cachedSender);
                var execResult = await _transactionProcessor.ExecuteTransactionAsync(
                    tx, blockContext, txIndexInBlock, cumulativeGasUsed, cachedSender);

                if (execResult.Skipped)
                    continue;

                execResults.Add(execResult);
                includedTransactions.Add(tx);
                txIndexInBlock++;

                var txHash = tx.Hash;
                var txResult = new TransactionResult
                {
                    TxHash = txHash,
                    Success = execResult.Success,
                    Receipt = execResult.Receipt,
                    ErrorMessage = execResult.RevertReason,
                    GasUsed = execResult.GasUsed,
                    ReturnData = execResult.ReturnData
                };

                results.Add(txResult);
                if (execResult.Receipt != null)
                {
                    receipts.Add(execResult.Receipt);
                    CombineBloom(combinedBloom, execResult.Receipt.Bloom);
                }
                encodedTransactions.Add(tx.GetRLPEncoded());
                cumulativeGasUsed = execResult.CumulativeGasUsed;

                if (execResult.Success)
                    successCount++;
                else
                    failCount++;
            }

            var transactionsRoot = includedTransactions.Count > 0
                ? _rootCalculator.CalculateTransactionsRoot(encodedTransactions, _trieNodeStore)
                : DefaultValues.EMPTY_TRIE_HASH;

            var receiptsRoot = receipts.Count > 0
                ? _rootCalculator.CalculateReceiptsRoot(receipts, _trieNodeStore)
                : DefaultValues.EMPTY_TRIE_HASH;

            var parentHash = latestBlock != null
                ? (await _blockStore.GetHashByNumberAsync(latestBlock.BlockNumber) ?? new byte[32])
                : new byte[32];

            var stateRoot = await _stateRootCalculator.ComputeStateRootAsync();
            _trieNodeStore?.Flush();

            var blockHeader = new BlockHeader
            {
                ParentHash = parentHash ?? new byte[32],
                UnclesHash = EMPTY_LIST_HASH,
                Coinbase = options.Coinbase,
                StateRoot = stateRoot,
                TransactionsHash = transactionsRoot,
                ReceiptHash = receiptsRoot,
                LogsBloom = combinedBloom,
                Difficulty = options.Difficulty,
                BlockNumber = nextBlockNumber,
                GasLimit = (long)options.BlockGasLimit,
                GasUsed = (long)cumulativeGasUsed,
                Timestamp = options.Timestamp,
                ExtraData = options.ExtraData ?? Array.Empty<byte>(),
                MixHash = options.PrevRandao ?? new byte[32],
                Nonce = options.Nonce ?? new byte[8],
                BaseFee = options.BaseFee
            };

            var blockHash = CalculateBlockHash(blockHeader);

            await _blockStore.SaveAsync(blockHeader, blockHash);

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var tx = includedTransactions[i];
                var execResult = execResults[i];

                await _transactionStore.SaveAsync(tx, blockHash, i, nextBlockNumber);

                if (result.Receipt != null)
                {
                    await _receiptStore.SaveAsync(
                        result.Receipt,
                        result.TxHash,
                        blockHash,
                        nextBlockNumber,
                        i,
                        execResult.GasUsed,
                        execResult.ContractAddress,
                        options.BaseFee);
                }

                if (execResult.Logs != null && execResult.Logs.Count > 0)
                {
                    await _logStore.SaveLogsAsync(
                        execResult.Logs,
                        result.TxHash,
                        blockHash,
                        nextBlockNumber,
                        i);
                }
            }

            await _logStore.SaveBlockBloomAsync(nextBlockNumber, combinedBloom);

            if (_stateStore is IHistoricalStateProvider historyProvider2)
                await historyProvider2.ClearCurrentBlockNumberAsync().ConfigureAwait(false);

            return new BlockProductionResult
            {
                Header = blockHeader,
                BlockHash = blockHash,
                TransactionResults = results,
                SuccessfulTransactions = successCount,
                FailedTransactions = failCount
            };
        }

        private static (IReadOnlyList<ISignedTransaction> ordered, Dictionary<int, string> senderCache) OrderTransactionsByNonce(IReadOnlyList<ISignedTransaction> transactions)
        {
            if (transactions.Count <= 1)
            {
                var cache = new Dictionary<int, string>();
                if (transactions.Count == 1)
                {
                    var sender = GetTransactionSender(transactions[0]);
                    if (sender != null) cache[0] = sender;
                }
                return (transactions, cache);
            }

            var grouped = new Dictionary<string, List<(int originalIndex, BigInteger nonce, ISignedTransaction tx, string? sender)>>(StringComparer.OrdinalIgnoreCase);
            var senderOrder = new List<string>();

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                var txData = TransactionProcessor.GetTransactionData(tx);
                var sender = GetTransactionSender(tx);
                var key = sender ?? $"_unknown_{i}";

                if (!grouped.TryGetValue(key, out var list))
                {
                    list = new List<(int, BigInteger, ISignedTransaction, string?)>();
                    grouped[key] = list;
                    senderOrder.Add(key);
                }
                list.Add((i, txData.Nonce, tx, sender));
            }

            var result = new List<ISignedTransaction>(transactions.Count);
            var senderCache = new Dictionary<int, string>();
            int outputIndex = 0;
            foreach (var senderKey in senderOrder)
            {
                var list = grouped[senderKey];
                list.Sort((a, b) => a.nonce.CompareTo(b.nonce));
                foreach (var entry in list)
                {
                    result.Add(entry.tx);
                    if (entry.sender != null)
                        senderCache[outputIndex] = entry.sender;
                    outputIndex++;
                }
            }

            return (result, senderCache);
        }

        private static string GetTransactionSender(ISignedTransaction tx)
        {
            try
            {
                var key = Signer.EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key?.GetPublicAddress();
            }
            catch
            {
                return null;
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
                PrevRandao = options.PrevRandao
            };
        }

        private void CombineBloom(byte[] target, byte[] source)
        {
            if (source == null || source.Length != 256) return;

            for (int i = 0; i < 256; i++)
            {
                target[i] |= source[i];
            }
        }

        private byte[] CalculateBlockHash(BlockHeader header)
        {
            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(header);
            return _keccak.CalculateHash(encoded);
        }

        private async Task ExecuteBeaconRootSystemCallAsync(long timestamp, byte[] parentBeaconBlockRoot)
        {
            var timestampIndex = timestamp % HISTORY_BUFFER_LENGTH;
            var rootIndex = timestampIndex + HISTORY_BUFFER_LENGTH;

            var timestampBytes = ((BigInteger)timestamp).ToBytesForRLPEncoding();
            await _stateStore.SaveStorageAsync(BEACON_ROOTS_ADDRESS, timestampIndex, timestampBytes);

            var rootValue = new BigInteger(parentBeaconBlockRoot, isUnsigned: true, isBigEndian: true);
            var rootBytes = rootValue.ToBytesForRLPEncoding();
            await _stateStore.SaveStorageAsync(BEACON_ROOTS_ADDRESS, rootIndex, rootBytes);
        }
    }
}
