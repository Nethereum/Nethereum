using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class BlockProducer : IBlockProducer
    {
        private static readonly byte[] EMPTY_LIST_HASH = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());

        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IStateStore _stateStore;
        private readonly TransactionProcessor _transactionProcessor;
        private readonly RootCalculator _rootCalculator;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly Sha3Keccack _keccak = new();

        public BlockProducer(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            TransactionProcessor transactionProcessor,
            ITrieNodeStore trieNodeStore = null)
        {
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _transactionProcessor = transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor));
            _trieNodeStore = trieNodeStore;
            _rootCalculator = new RootCalculator();
        }

        public async Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options)
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            var nextBlockNumber = latestBlock != null ? latestBlock.BlockNumber + 1 : 1;

            var blockContext = CreateBlockContext(nextBlockNumber, options);

            var results = new List<TransactionResult>();
            var execResults = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedTransactions = new List<byte[]>();
            BigInteger cumulativeGasUsed = 0;
            BigInteger totalGasUsed = 0;
            var combinedBloom = new byte[256];
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                var execResult = await _transactionProcessor.ExecuteTransactionAsync(
                    tx, blockContext, i, cumulativeGasUsed);
                execResults.Add(execResult);

                var txHash = tx.Hash;
                var txResult = new TransactionResult
                {
                    TxHash = txHash,
                    Success = execResult.Success,
                    Receipt = execResult.Receipt,
                    ErrorMessage = execResult.RevertReason
                };

                results.Add(txResult);
                if (execResult.Receipt != null)
                {
                    receipts.Add(execResult.Receipt);
                    CombineBloom(combinedBloom, execResult.Receipt.Bloom);
                }
                encodedTransactions.Add(tx.GetRLPEncoded());
                totalGasUsed += execResult.GasUsed;
                cumulativeGasUsed = execResult.CumulativeGasUsed;

                if (execResult.Success)
                    successCount++;
                else
                    failCount++;
            }

            var transactionsRoot = transactions.Count > 0
                ? _rootCalculator.CalculateTransactionsRoot(encodedTransactions, _trieNodeStore)
                : DefaultValues.EMPTY_TRIE_HASH;

            var receiptsRoot = receipts.Count > 0
                ? _rootCalculator.CalculateReceiptsRoot(receipts, _trieNodeStore)
                : DefaultValues.EMPTY_TRIE_HASH;

            var parentHash = latestBlock != null
                ? await _blockStore.GetHashByNumberAsync(latestBlock.BlockNumber)
                : new byte[32];

            var stateRoot = await ComputeStateRootAsync();

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
                GasUsed = (long)totalGasUsed,
                Timestamp = options.Timestamp,
                ExtraData = options.ExtraData ?? Array.Empty<byte>(),
                MixHash = options.PrevRandao ?? new byte[32],
                Nonce = new byte[8],
                BaseFee = options.BaseFee
            };

            var blockHash = CalculateBlockHash(blockHeader);

            await _blockStore.SaveAsync(blockHeader, blockHash);

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var tx = transactions[i];
                var execResult = execResults[i];

                await _transactionStore.SaveAsync(tx, blockHash, i);

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

            return new BlockProductionResult
            {
                Header = blockHeader,
                BlockHash = blockHash,
                TransactionResults = results,
                SuccessfulTransactions = successCount,
                FailedTransactions = failCount
            };
        }

        private BlockContext CreateBlockContext(BigInteger blockNumber, BlockProductionOptions options)
        {
            return new BlockContext
            {
                BlockNumber = blockNumber,
                Timestamp = options.Timestamp,
                GasLimit = options.BlockGasLimit,
                BaseFee = options.BaseFee,
                Coinbase = options.Coinbase,
                ChainId = 0,
                Difficulty = options.Difficulty,
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

        private async Task<byte[]> ComputeStateRootAsync()
        {
            var accounts = await _stateStore.GetAllAccountsAsync();
            if (accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var sha3 = new Sha3Keccack();
            var accountsWithHashedKeys = new Dictionary<byte[], Account>();

            foreach (var kvp in accounts)
            {
                var addressBytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(kvp.Key);
                if (addressBytes.Length < 20)
                {
                    var paddedAddress = new byte[20];
                    Array.Copy(addressBytes, 0, paddedAddress, 20 - addressBytes.Length, addressBytes.Length);
                    addressBytes = paddedAddress;
                }
                var hashedKey = sha3.CalculateHash(addressBytes);

                var storage = await _stateStore.GetAllStorageAsync(kvp.Key);
                var account = new Account
                {
                    Nonce = kvp.Value.Nonce,
                    Balance = kvp.Value.Balance,
                    CodeHash = kvp.Value.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
                };

                if (storage.Count > 0)
                {
                    var storageDict = new Dictionary<byte[], byte[]>();
                    foreach (var storageKvp in storage)
                    {
                        var slotBytes = storageKvp.Key.ToBytesForRLPEncoding();
                        if (slotBytes.Length < 32)
                        {
                            var paddedSlot = new byte[32];
                            Array.Copy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
                            slotBytes = paddedSlot;
                        }
                        var hashedSlot = sha3.CalculateHash(slotBytes);
                        storageDict[hashedSlot] = storageKvp.Value;
                    }
                    account.StateRoot = _rootCalculator.CalculateStorageRoot(storageDict, _trieNodeStore);
                }
                else
                {
                    account.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }

                accountsWithHashedKeys[hashedKey] = account;
            }

            return _rootCalculator.CalculateStateRoot(accountsWithHashedKeys, _trieNodeStore);
        }
    }
}
