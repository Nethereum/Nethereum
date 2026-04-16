using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class BlockManager : IDisposable, IAsyncDisposable
    {
        private const int MaxExecutionResultsCache = 10000;
        private static readonly byte[] EMPTY_LIST_HASH = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());
        private readonly Sha3Keccack _keccak = new();

        private readonly IBlockStore _blockStore;
        private readonly IStateStore _stateStore;
        private readonly ITransactionVerificationAndRecovery _txVerifier;
        private readonly DevChainConfig _config;
        private readonly IBlockProducer _blockProducer;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly SemaphoreSlim _mineLock = new SemaphoreSlim(1, 1);

        private readonly object _pendingLock = new object();
        private volatile List<ISignedTransaction> _pendingTransactionsList = new();
        private volatile HashSet<string> _pendingHashes = new();
        private Dictionary<string, BigInteger> _pendingNonces = new(StringComparer.OrdinalIgnoreCase);
        private volatile CoreChain.BlockContext _pendingBlockContext;
        private readonly ConcurrentDictionary<string, TransactionExecutionResult> _lastExecutionResults = new();

        private CancellationTokenSource _mineLoopCts;
        private Task _mineLoopTask;

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
            _trieNodeStore = trieNodeStore;

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

            if (_config.AutoMine && _config.AutoMineBatchSize > 1)
            {
                StartMineLoop();
            }
        }

        private void StartMineLoop()
        {
            _mineLoopCts = new CancellationTokenSource();
            var ct = _mineLoopCts.Token;
            var intervalMs = _config.AutoMineBatchTimeoutMs > 0 ? _config.AutoMineBatchTimeoutMs : 10;

            _mineLoopTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        bool hasPending;
                        lock (_pendingLock) { hasPending = _pendingTransactionsList.Count > 0; }

                        if (hasPending)
                        {
                            await MineBlockAsync();
                        }

                        await Task.Delay(intervalMs, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"BlockManager mine loop error: {ex.Message}");
                    }
                }
            }, ct);
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
            _trieNodeStore?.Flush();
        }

        private async Task InitializePendingBlockAsync()
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            var nextBlockNumber = latestBlock != null ? latestBlock.BlockNumber + 1 : 1;
            var timestamp = DateTime.UtcNow.ToUnixTimestamp() + _config.TimeOffset;

            _pendingBlockContext = CoreChain.BlockContext.FromConfig(_config, nextBlockNumber, timestamp);
            _pendingBlockContext.BaseFee = _config.BaseFee;
        }


        public void AddPendingTransaction(ISignedTransaction tx)
        {
            var txHash = tx.Hash;
            if (txHash == null) return;

            var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();

            lock (_pendingLock)
            {
                if (_pendingTransactionsList.Count >= _config.MaxTransactionsPerBlock)
                    return;

                if (_pendingHashes.Add(hashKey))
                {
                    _pendingTransactionsList.Add(tx);
                }
            }
        }

        public async Task<byte[]> MineBlockAsync() => await MineBlockAsync(null);

        public async Task<byte[]> MineBlockAsync(byte[] parentBeaconBlockRoot)
        {
            await _mineLock.WaitAsync();
            try
            {
                return await MineBlockInternalAsync(parentBeaconBlockRoot);
            }
            finally
            {
                _mineLock.Release();
            }
        }

        private async Task<byte[]> MineBlockInternalAsync(byte[] parentBeaconBlockRoot)
        {
            List<ISignedTransaction> transactions;
            lock (_pendingLock)
            {
                transactions = _pendingTransactionsList;
                _pendingTransactionsList = new List<ISignedTransaction>();
                _pendingHashes = new HashSet<string>();
                _pendingNonces = new Dictionary<string, BigInteger>(StringComparer.OrdinalIgnoreCase);
            }

            var blockContext = _pendingBlockContext;
            var overrides = _config.ConsumeNextBlockOverrides();
            var timestamp = overrides.Timestamp ?? (DateTime.UtcNow.ToUnixTimestamp() + _config.TimeOffset);
            var baseFee = overrides.BaseFee ?? _config.BaseFee;
            var prevRandao = overrides.PrevRandao ?? blockContext.PrevRandao;
            var coinbase = overrides.Coinbase ?? _config.Coinbase;

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

            if (_lastExecutionResults.Count > MaxExecutionResultsCache)
            {
                var toRemove = _lastExecutionResults.Keys.Take(_lastExecutionResults.Count - MaxExecutionResultsCache / 2).ToList();
                foreach (var key in toRemove)
                {
                    _lastExecutionResults.TryRemove(key, out _);
                }
            }

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

            await InitializePendingBlockAsync();

            return result.BlockHash;
        } // end MineBlockInternalAsync

        public async Task<byte[]> MineBlockWithTransactionAsync(ISignedTransaction tx)
        {
            AddPendingTransaction(tx);
            return await MineBlockAsync();
        }

        public async Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
        {
            if (_config.AutoMineBatchSize > 1)
            {
                AddPendingTransaction(tx);
                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    TransactionHash = tx.Hash,
                    Success = true
                };
            }

            var validationResult = await ValidateTransactionAsync(tx);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            AddPendingTransaction(tx);

            if (_config.AutoMine)
            {
                var txHash = tx.Hash.ToHex(true);
                await MineBlockAsync();

                if (_lastExecutionResults.TryGetValue(txHash, out var executionResult))
                {
                    return executionResult;
                }

                return new TransactionExecutionResult
                {
                    Transaction = tx,
                    TransactionHash = tx.Hash,
                    Success = false,
                    RevertReason = "Transaction was not included in the mined block"
                };
            }

            return new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = tx.Hash,
                Success = true
            };
        }

        private async Task<TransactionExecutionResult> ValidateTransactionAsync(ISignedTransaction tx)
        {
            var result = new TransactionExecutionResult
            {
                Transaction = tx,
                TransactionHash = tx.Hash
            };

            var senderAddress = _txVerifier.GetSenderAddress(tx);
            if (string.IsNullOrEmpty(senderAddress))
            {
                result.Success = false;
                result.RevertReason = "Invalid signature: cannot recover sender address";
                return result;
            }

            var txData = CoreChain.TransactionProcessor.GetTransactionData(tx);
            var isContractCreation = string.IsNullOrEmpty(txData.To);

            var intrinsicGas = CoreChain.TransactionProcessor.CalculateIntrinsicGas(txData.Data, isContractCreation);
            if (txData.GasLimit.ToBigInteger() < intrinsicGas)
            {
                result.Success = false;
                result.RevertReason = $"Intrinsic gas too low: have {txData.GasLimit}, want {intrinsicGas}";
                return result;
            }

            var senderAccount = await _stateStore.GetAccountAsync(senderAddress);
            if (senderAccount == null)
            {
                senderAccount = new Account { Balance = 0, Nonce = 0 };
            }

            lock (_pendingLock)
            {
                var expectedNonce = _pendingNonces.TryGetValue(senderAddress, out var pendingNonce)
                    ? pendingNonce
                    : senderAccount.Nonce.ToBigInteger();

                if (expectedNonce != txData.Nonce.ToBigInteger())
                {
                    result.Success = false;
                    result.RevertReason = $"Invalid nonce: have {txData.Nonce}, want {expectedNonce}";
                    return result;
                }

                var maxCost = txData.GasLimit * txData.GasPrice + txData.Value;
                if (senderAccount.Balance < maxCost)
                {
                    result.Success = false;
                    result.RevertReason = $"Insufficient funds: have {senderAccount.Balance}, want {maxCost}";
                    return result;
                }

                _pendingNonces[senderAddress] = (txData.Nonce + 1).ToBigInteger();
            }

            result.Success = true;
            return result;
        }

        public int GetPendingTransactionCount()
        {
            lock (_pendingLock) { return _pendingTransactionsList.Count; }
        }

        public List<ISignedTransaction> GetPendingTransactions()
        {
            lock (_pendingLock) { return _pendingTransactionsList.ToList(); }
        }

        public async Task ReinitializePendingBlockAsync()
        {
            await _mineLock.WaitAsync();
            try
            {
                lock (_pendingLock)
                {
                    _pendingTransactionsList = new List<ISignedTransaction>();
                    _pendingHashes = new HashSet<string>();
                    _pendingNonces = new Dictionary<string, BigInteger>(StringComparer.OrdinalIgnoreCase);
                }
                await InitializePendingBlockAsync();
            }
            finally
            {
                _mineLock.Release();
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
            return _keccak.CalculateHash(encoded);
        }

        public async ValueTask DisposeAsync()
        {
            _mineLoopCts?.Cancel();
            if (_mineLoopTask != null)
            {
                try { await _mineLoopTask.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (OperationCanceledException) { }
                catch (TimeoutException) { }
            }
            _mineLoopCts?.Dispose();
            _mineLock.Dispose();
        }

        public void Dispose()
        {
            _mineLoopCts?.Cancel();
            if (_mineLoopTask != null)
            {
                try { _mineLoopTask.Wait(TimeSpan.FromSeconds(2)); }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }
            }
            _mineLoopCts?.Dispose();
            _mineLock.Dispose();
        }
    }
}
