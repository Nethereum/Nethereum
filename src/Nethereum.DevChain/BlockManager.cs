using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
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
        private readonly IStateStore _stateStore;
        private readonly ITransactionVerificationAndRecovery _txVerifier;
        private readonly DevChainConfig _config;
        private readonly IBlockProducer _blockProducer;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _mineLock = new SemaphoreSlim(1, 1);

        private List<ISignedTransaction> _pendingTransactions = new List<ISignedTransaction>();
        private CoreChain.BlockContext _pendingBlockContext;
        private Dictionary<string, TransactionExecutionResult> _lastExecutionResults = new Dictionary<string, TransactionExecutionResult>();

        // Batch mining support
        private readonly ConcurrentDictionary<string, TaskCompletionSource<TransactionExecutionResult>> _pendingResultSources = new();
        private Timer _batchTimer;
        private int _batchPending;

        public BlockManager(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            CoreChain.TransactionProcessor transactionProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            DevChainConfig config,
            ITrieNodeStore trieNodeStore = null)
        {
            _blockStore = blockStore;
            _stateStore = stateStore;
            _txVerifier = txVerifier;
            _config = config;

            _blockProducer = new BlockProducer(
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                transactionProcessor,
                trieNodeStore);
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
            var stateRootCalculator = new StateRootCalculator();
            var stateRoot = await stateRootCalculator.ComputeStateRootAsync(_stateStore);

            var genesisHeader = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = EMPTY_LIST_HASH,
                Coinbase = _config.Coinbase,
                StateRoot = stateRoot,
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
            var baseFee = GetNextBlockBaseFee();

            _pendingBlockContext = CoreChain.BlockContext.FromConfig(_config, nextBlockNumber, timestamp);
            _pendingBlockContext.BaseFee = baseFee;
        }

        private BigInteger GetNextBlockBaseFee()
        {
            if (_config.NextBlockBaseFee.HasValue)
            {
                var baseFee = _config.NextBlockBaseFee.Value;
                _config.NextBlockBaseFee = null;
                return baseFee;
            }
            return _config.BaseFee;
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

        private byte[] GetNextBlockPrevRandao()
        {
            if (_config.NextBlockPrevRandao != null)
            {
                var prevRandao = _config.NextBlockPrevRandao;
                _config.NextBlockPrevRandao = null;
                return prevRandao;
            }
            return null;
        }

        private string GetNextBlockCoinbase()
        {
            if (_config.NextBlockCoinbase != null)
            {
                var coinbase = _config.NextBlockCoinbase;
                _config.NextBlockCoinbase = null;
                return coinbase;
            }
            return _config.Coinbase;
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

        public async Task<byte[]> MineBlockAsync() => await MineBlockAsync(null);

        public async Task<byte[]> MineBlockAsync(byte[] parentBeaconBlockRoot)
        {
            List<ISignedTransaction> transactions;
            BlockContext blockContext;

            lock (_lock)
            {
                transactions = _pendingTransactions.ToList();
                _pendingTransactions.Clear();
                blockContext = _pendingBlockContext;
            }

            // Skip if no transactions (another concurrent mine already processed them)
            if (transactions.Count == 0)
            {
                return null;
            }

            var timestamp = GetNextBlockTimestamp();
            var baseFee = GetNextBlockBaseFee();
            var prevRandao = GetNextBlockPrevRandao() ?? blockContext.PrevRandao;
            var coinbase = GetNextBlockCoinbase();

            var options = new BlockProductionOptions
            {
                Timestamp = timestamp,
                BlockGasLimit = blockContext.GasLimit,
                BaseFee = baseFee,
                Coinbase = coinbase,
                Difficulty = blockContext.Difficulty,
                PrevRandao = prevRandao,
                ExtraData = Array.Empty<byte>(),
                ChainId = blockContext.ChainId,
                ParentBeaconBlockRoot = parentBeaconBlockRoot
            };

            var result = await _blockProducer.ProduceBlockAsync(transactions, options);

            // Track execution results for DevChain-specific features (AutoMine result lookup)
            // Note: Results accumulate intentionally - don't clear to avoid race conditions
            lock (_lock)
            {
                foreach (var txResult in result.TransactionResults)
                {
                    var txHash = txResult.TxHash.ToHex(true);
                    var receipt = txResult.Receipt;
                    var logs = receipt?.Logs ?? new List<Log>();
                    _lastExecutionResults[txHash] = new TransactionExecutionResult
                    {
                        TransactionHash = txResult.TxHash,
                        Success = txResult.Success,
                        Receipt = receipt,
                        Logs = logs,
                        RevertReason = txResult.ErrorMessage,
                        GasUsed = txResult.GasUsed,
                        ReturnData = txResult.ReturnData
                    };
                }
            }

            await InitializePendingBlockAsync();

            return result.BlockHash;
        }

        public async Task<byte[]> MineBlockWithTransactionAsync(ISignedTransaction tx)
        {
            AddPendingTransaction(tx);
            return await MineBlockAsync();
        }

        public async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            // Step 1: Mempool validation (like real Ethereum)
            var validationResult = await ValidateTransactionAsync(tx);
            if (!validationResult.Success)
            {
                return validationResult; // RPC error for mempool failures
            }

            // Step 2: Add to pending (mempool accepted)
            AddPendingTransaction(tx);

            // Step 3: Mine if AutoMine
            if (_config.AutoMine)
            {
                var txHash = tx.Hash.ToHex(true);

                // Batch mode: batch size > 1
                if (_config.AutoMineBatchSize > 1)
                {
                    return await SendTransactionBatchedAsync(tx, txHash);
                }

                // Immediate mode: batch size == 1 (legacy behavior)
                await MineBlockAsync();

                lock (_lock)
                {
                    if (_lastExecutionResults.TryGetValue(txHash, out var executionResult))
                    {
                        return executionResult;
                    }
                }
            }

            // Step 4: Return SUCCESS with tx hash (like real Ethereum)
            // Execution success/failure is in receipt.status, not here
            return new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = tx.Hash,
                Success = true // Mempool accepted = success for eth_sendRawTransaction
            };
        }

        private async Task<TransactionExecutionResult> SendTransactionBatchedAsync(ISignedTransaction tx, string txHash)
        {
            // Create completion source for this transaction
            var tcs = new TaskCompletionSource<TransactionExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingResultSources[txHash] = tcs;

            bool shouldMine = false;
            lock (_lock)
            {
                _batchPending++;

                // Check if batch is full
                if (_batchPending >= _config.AutoMineBatchSize)
                {
                    shouldMine = true;
                    _batchPending = 0;
                    _batchTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                }
                else
                {
                    // Start or reset batch timer
                    if (_batchTimer == null)
                    {
                        _batchTimer = new Timer(OnBatchTimeout, null, _config.AutoMineBatchTimeoutMs, Timeout.Infinite);
                    }
                    else
                    {
                        _batchTimer.Change(_config.AutoMineBatchTimeoutMs, Timeout.Infinite);
                    }
                }
            }

            if (shouldMine)
            {
                await MineAndCompleteAsync();
            }

            // Wait for result (with timeout to prevent deadlocks)
            var timeoutTask = Task.Delay(30000); // 30 second timeout
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingResultSources.TryRemove(txHash, out _);
                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    TransactionHash = tx.Hash,
                    Success = false,
                    RevertReason = "Batch mining timeout"
                };
            }

            return await tcs.Task;
        }

        private void OnBatchTimeout(object state)
        {
            lock (_lock)
            {
                if (_batchPending > 0)
                {
                    _batchPending = 0;
                    // Fire and forget the mining task
                    _ = MineAndCompleteAsync();
                }
            }
        }

        private async Task MineAndCompleteAsync()
        {
            await MineBlockAsync();

            // Complete all pending TaskCompletionSources
            var toComplete = _pendingResultSources.Keys.ToList();
            foreach (var txHash in toComplete)
            {
                if (_pendingResultSources.TryRemove(txHash, out var tcs))
                {
                    TransactionExecutionResult result;
                    lock (_lock)
                    {
                        if (!_lastExecutionResults.TryGetValue(txHash, out result))
                        {
                            // TX was in pending but not mined (possible if another mine grabbed it)
                            result = new TransactionExecutionResult
                            {
                                TransactionHash = txHash.HexToByteArray(),
                                Success = true
                            };
                        }
                    }
                    tcs.TrySetResult(result);
                }
            }
        }

        private async Task<TransactionExecutionResult> ValidateTransactionAsync(ISignedTransaction tx)
        {
            var result = new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = tx.Hash
            };

            // 1. Verify signature and recover sender
            var senderAddress = _txVerifier.GetSenderAddress(tx);
            if (string.IsNullOrEmpty(senderAddress))
            {
                result.Success = false;
                result.RevertReason = "Invalid signature: cannot recover sender address";
                return result;
            }

            // 2. Get transaction data
            var txData = CoreChain.TransactionProcessor.GetTransactionData(tx);
            var isContractCreation = string.IsNullOrEmpty(txData.To);

            // 3. Check intrinsic gas
            var intrinsicGas = CoreChain.TransactionProcessor.CalculateIntrinsicGas(txData.Data, isContractCreation);
            if (txData.GasLimit < intrinsicGas)
            {
                result.Success = false;
                result.RevertReason = $"Intrinsic gas too low: have {txData.GasLimit}, want {intrinsicGas}";
                return result;
            }

            // 4. Check sender account
            var senderAccount = await _stateStore.GetAccountAsync(senderAddress);
            if (senderAccount == null)
            {
                senderAccount = new Account { Balance = 0, Nonce = 0 };
            }

            // 5. Check nonce
            if (senderAccount.Nonce != txData.Nonce)
            {
                result.Success = false;
                result.RevertReason = $"Invalid nonce: have {txData.Nonce}, want {senderAccount.Nonce}";
                return result;
            }

            // 6. Check balance
            var maxCost = txData.GasLimit * txData.GasPrice + txData.Value;
            if (senderAccount.Balance < maxCost)
            {
                result.Success = false;
                result.RevertReason = $"Insufficient funds: have {senderAccount.Balance}, want {maxCost}";
                return result;
            }

            result.Success = true;
            return result;
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

        public CoreChain.BlockContext GetPendingBlockContext()
        {
            return _pendingBlockContext;
        }

        private byte[] CalculateBlockHash(BlockHeader header)
        {
            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(header);
            return new Sha3Keccack().CalculateHash(encoded);
        }
    }
}
