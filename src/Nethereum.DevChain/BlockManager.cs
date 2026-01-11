using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.DevChain
{
    public class BlockManager
    {
        private static readonly byte[] EMPTY_LIST_HASH = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());

        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IStateStore _stateStore;
        private readonly TransactionProcessor _transactionProcessor;
        private readonly DevChainConfig _config;
        private readonly RootCalculator _rootCalculator;
        private readonly object _lock = new object();

        private List<ISignedTransaction> _pendingTransactions = new List<ISignedTransaction>();
        private BlockContext _pendingBlockContext;

        public BlockManager(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            TransactionProcessor transactionProcessor,
            DevChainConfig config)
        {
            _blockStore = blockStore;
            _transactionStore = transactionStore;
            _receiptStore = receiptStore;
            _logStore = logStore;
            _stateStore = stateStore;
            _transactionProcessor = transactionProcessor;
            _config = config;
            _rootCalculator = new RootCalculator();
        }

        public async Task InitializeAsync()
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            if (latestBlock == null)
            {
                await CreateGenesisBlockAsync();
            }

            await InitializePendingBlockAsync();
        }

        private async Task CreateGenesisBlockAsync()
        {
            var genesisHeader = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = EMPTY_LIST_HASH,
                Coinbase = _config.Coinbase,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Difficulty = 1,
                BlockNumber = 0,
                GasLimit = (long)_config.BlockGasLimit,
                GasUsed = 0,
                Timestamp = DateTime.UtcNow.ToUnixTimestamp(),
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = _config.BaseFee
            };

            var genesisHash = CalculateBlockHash(genesisHeader);
            await _blockStore.SaveAsync(genesisHeader, genesisHash);
        }

        private async Task InitializePendingBlockAsync()
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            var nextBlockNumber = latestBlock != null ? latestBlock.BlockNumber + 1 : 1;
            var timestamp = GetNextBlockTimestamp();

            _pendingBlockContext = BlockContext.FromConfig(_config, nextBlockNumber, timestamp);
        }

        private long GetNextBlockTimestamp()
        {
            if (_config.NextBlockTimestamp.HasValue)
            {
                var timestamp = _config.NextBlockTimestamp.Value;
                _config.NextBlockTimestamp = null;
                return timestamp;
            }
            return DateTime.UtcNow.ToUnixTimestamp() + _config.TimeOffset;
        }

        public void AddPendingTransaction(ISignedTransaction tx)
        {
            lock (_lock)
            {
                if (_pendingTransactions.Count >= _config.MaxTransactionsPerBlock)
                    return;

                _pendingTransactions.Add(tx);
            }
        }

        public async Task<byte[]> MineBlockAsync()
        {
            List<ISignedTransaction> transactions;
            BlockContext blockContext;

            lock (_lock)
            {
                transactions = _pendingTransactions.ToList();
                _pendingTransactions.Clear();
                blockContext = _pendingBlockContext;
            }

            blockContext.Timestamp = GetNextBlockTimestamp();

            var results = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedTransactions = new List<byte[]>();
            BigInteger cumulativeGasUsed = 0;
            BigInteger totalGasUsed = 0;
            var combinedBloom = new byte[256];

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                var result = await _transactionProcessor.ExecuteTransactionAsync(
                    tx, blockContext, i, cumulativeGasUsed);

                results.Add(result);
                receipts.Add(result.Receipt);
                encodedTransactions.Add(tx.GetRLPEncoded());
                totalGasUsed += result.GasUsed;
                cumulativeGasUsed = result.CumulativeGasUsed;

                CombineBloom(combinedBloom, result.Receipt.Bloom);
            }

            var transactionsRoot = _rootCalculator.CalculateTransactionsRoot(encodedTransactions);
            var receiptsRoot = _rootCalculator.CalculateReceiptsRoot(receipts);

            var parentBlock = await _blockStore.GetLatestAsync();
            var parentHash = parentBlock != null
                ? await _blockStore.GetHashByNumberAsync(parentBlock.BlockNumber)
                : new byte[32];

            var stateRoot = await ComputeStateRootAsync();

            var blockHeader = new BlockHeader
            {
                ParentHash = parentHash ?? new byte[32],
                UnclesHash = EMPTY_LIST_HASH,
                Coinbase = blockContext.Coinbase,
                StateRoot = stateRoot,
                TransactionsHash = transactionsRoot,
                ReceiptHash = receiptsRoot,
                LogsBloom = combinedBloom,
                Difficulty = blockContext.Difficulty,
                BlockNumber = blockContext.BlockNumber,
                GasLimit = (long)blockContext.GasLimit,
                GasUsed = (long)totalGasUsed,
                Timestamp = blockContext.Timestamp,
                ExtraData = new byte[0],
                MixHash = blockContext.PrevRandao ?? new byte[32],
                Nonce = new byte[8],
                BaseFee = blockContext.BaseFee
            };

            var blockHash = CalculateBlockHash(blockHeader);

            await _blockStore.SaveAsync(blockHeader, blockHash);

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var tx = transactions[i];

                var effectiveGasPrice = CalculateEffectiveGasPrice(tx, blockContext.BaseFee);

                await _transactionStore.SaveAsync(tx, blockHash, i);
                await _receiptStore.SaveAsync(result.Receipt, tx.Hash, blockHash, blockContext.BlockNumber, i, result.GasUsed, result.ContractAddress, effectiveGasPrice);

                if (result.Logs != null && result.Logs.Count > 0)
                {
                    await _logStore.SaveLogsAsync(
                        result.Logs,
                        tx.Hash,
                        blockHash,
                        blockContext.BlockNumber,
                        i);
                }
            }

            await InitializePendingBlockAsync();

            return blockHash;
        }

        public async Task<byte[]> MineBlockWithTransactionAsync(ISignedTransaction tx)
        {
            AddPendingTransaction(tx);
            return await MineBlockAsync();
        }

        public async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            AddPendingTransaction(tx);

            if (_config.AutoMine)
            {
                await MineBlockAsync();
            }

            var location = await _transactionStore.GetLocationAsync(tx.Hash);
            if (location != null)
            {
                var blockHeader = await _blockStore.GetByHashAsync(location.BlockHash);
                var receipt = await _receiptStore.GetByTxHashAsync(tx.Hash);

                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    TransactionHash = tx.Hash,
                    TransactionIndex = location.TransactionIndex,
                    Success = receipt?.HasSucceeded ?? false,
                    Receipt = receipt,
                    Logs = receipt?.Logs ?? new List<Log>()
                };
            }

            return new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = tx.Hash,
                Success = false,
                RevertReason = "Transaction not yet mined"
            };
        }

        public int GetPendingTransactionCount()
        {
            lock (_lock)
            {
                return _pendingTransactions.Count;
            }
        }

        public List<ISignedTransaction> GetPendingTransactions()
        {
            lock (_lock)
            {
                return _pendingTransactions.ToList();
            }
        }

        public BlockContext GetPendingBlockContext()
        {
            return _pendingBlockContext;
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
            return new Sha3Keccack().CalculateHash(encoded);
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
                var addressBytes = kvp.Key.HexToByteArray();
                if (addressBytes.Length < 20)
                {
                    var paddedAddress = new byte[20];
                    System.Array.Copy(addressBytes, 0, paddedAddress, 20 - addressBytes.Length, addressBytes.Length);
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
                            System.Array.Copy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
                            slotBytes = paddedSlot;
                        }
                        var hashedSlot = sha3.CalculateHash(slotBytes);
                        storageDict[hashedSlot] = storageKvp.Value;
                    }
                    account.StateRoot = _rootCalculator.CalculateStorageRoot(storageDict);
                }
                else
                {
                    account.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }

                accountsWithHashedKeys[hashedKey] = account;
            }

            return _rootCalculator.CalculateStateRoot(accountsWithHashedKeys);
        }

        private BigInteger CalculateEffectiveGasPrice(ISignedTransaction tx, BigInteger baseFee)
        {
            if (tx is Transaction1559 eip1559Tx)
            {
                var maxPriorityFee = eip1559Tx.MaxPriorityFeePerGas ?? BigInteger.Zero;
                var maxFee = eip1559Tx.MaxFeePerGas ?? BigInteger.Zero;
                var priorityFee = BigInteger.Min(maxPriorityFee, maxFee - baseFee);
                return baseFee + priorityFee;
            }
            if (tx is Transaction2930 eip2930Tx)
            {
                return eip2930Tx.GasPrice ?? BigInteger.Zero;
            }
            if (tx is LegacyTransaction legacyTx)
            {
                return legacyTx.GasPrice.ToBigIntegerFromRLPDecoded();
            }
            if (tx is LegacyTransactionChainId legacyChainIdTx)
            {
                return legacyChainIdTx.GasPrice.ToBigIntegerFromRLPDecoded();
            }
            return baseFee;
        }
    }
}
