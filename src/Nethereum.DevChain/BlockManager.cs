using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevChain
{
    public class BlockManager : IDisposable, IAsyncDisposable
    {
        private const int MaxExecutionResultsCache = 10000;

        private readonly IBlockStore _blockStore;
        private readonly IStateStore _stateStore;
        private readonly ITransactionVerificationAndRecovery _txVerifier;
        private readonly DevChainConfig _config;
        private readonly IBlockProducer _blockProducer;
        private readonly CoreChain.BlockExecutor _engine;
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

        /// <summary>
        /// Whether headers on this chain carry the EIP-7928 block access list hash and
        /// the EIP-7843 slot number. One predicate for both, because the pair is emitted
        /// together or not at all — asking it through the header codec keeps the answer
        /// where the encoding rule already lives instead of restating a fork ordering.
        /// </summary>
        private bool CarriesAmsterdamHeaderFieldsAt(long blockNumber, ulong timestamp) =>
            Model.Codecs.BlockHeaderCodecs
                .ForFork(_config.ResolveActivations().ResolveAt(blockNumber, timestamp))
                .CarriesBlockAccessList;

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
            : this(blockStore, transactionStore, receiptStore, logStore, stateStore,
                   transactionProcessor, txVerifier, config, trieNodeStore, blockAccessListStore: null)
        {
        }

        public BlockManager(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            CoreChain.TransactionProcessor transactionProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            DevChainConfig config,
            ITrieNodeStore trieNodeStore,
            IBlockAccessListStore blockAccessListStore)
            : this(blockStore, transactionStore, receiptStore, logStore, stateStore,
                   transactionProcessor, txVerifier, config, trieNodeStore, blockAccessListStore, uncleStore: null)
        {
        }

        public BlockManager(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            CoreChain.TransactionProcessor transactionProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            DevChainConfig config,
            ITrieNodeStore trieNodeStore,
            IBlockAccessListStore blockAccessListStore,
            IUncleStore uncleStore)
        {
            _blockStore = blockStore;
            _stateStore = stateStore;
            _txVerifier = txVerifier;
            _config = config;
            _trieNodeStore = trieNodeStore ?? new InMemoryContentNodeStore();

            var activations = _config.ResolveActivations();
            var core = ExecutionStackFactory.BuildExecutor(
                stateStore, blockStore, _trieNodeStore, _trieNodeStore, uncleStore,
                activations,
                chainConfigFactory: _ => config,
                hardforkConfigFactory: config.ConfigForFork,
                options: new ExecutionStackOptions
                {
                    RewardPolicy = config.RewardPolicy ?? NoRewardPolicy.Instance,
                    StateRootCalculatorFactory = config.CreateIncrementalStateRootCalculatorForStateTree,
                    TransactionVerifier = txVerifier
                });
            StateRootCalculator = core.Calculator;
            var stateRootCalc = core.Calculator;
            var engine = core.Executor;
            _engine = engine;

            // EIP-7928 / AMS-7928-06. DevChain is deliberately Amsterdam-capable -- the fork is
            // user-configurable through DevChainConfig.Hardfork, CarriesAmsterdamHeaderFields
            // stamps the slot number, and genesis predeploys the system contracts per fork. So
            // when it is pinned to Amsterdam it MINTS a block access list and commits
            // keccak(rlp(bal)) into the header. The authoring node holds the only copy of that
            // list in existence: unlike uncles or withdrawals, which a peer can re-serve from a
            // block body forever, a BAL exists nowhere else once execution ends. The in-memory
            // fallback keeps that copy for the life of the process for callers who supply no
            // store; a node that outlives its process must pass a durable one.
            _blockProducer = new BlockProducer(
                engine,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                _trieNodeStore,
                stateRootCalc,
                orderingPolicy: null,
                blockHashProvider: null,
                blockEncodingProvider: null,
                blockRootsProvider: null,
                withdrawalStore: null,
                nodeCommitBlockContext: null,
                blockAccessListStore: blockAccessListStore ?? new CoreChain.Storage.InMemory.InMemoryBlockAccessListStore(blockStore),
                hardforkConfigFactory: config.ConfigForFork);
        }

        public async Task InitializeAsync()
        {
            var latestBlock = await _blockStore.GetLatestAsync();
            if (latestBlock == null)
            {
                await CreateGenesisBlockAsync();
            }
            else
            {
                await EnsureStoredGenesisMatchesPinnedForkAsync();
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

        private async Task EnsureStoredGenesisMatchesPinnedForkAsync()
        {
            var storedGenesis = await _blockStore.GetByNumberAsync(0);
            if (storedGenesis == null)
                throw new InvalidOperationException(
                    "This store holds blocks but no genesis, so nothing states which chain they belong to.");

            GenesisHeaderFields.EnsureShapeMatchesPinnedFork(storedGenesis, _config.PinnedFork);
        }

        private async Task CreateGenesisBlockAsync()
        {
            // EIP-2935 / EIP-4788 expect their system contracts to be in state
            // already when the fork activates — mainnet deployed both before
            // their fork block and every EEST fixture ships them in `pre:`.
            // DevChain pins a fork in config, so genesis is where they have to
            // come from; without them BLOCKHASH at Prague+ reads an empty
            // history contract and silently answers zero.
            if (_config.PredeploySystemContracts)
                await SystemContractPredeploys.ApplyGenesisAllocationAsync(
                    _stateStore, _config.PinnedFork);

            var stateRoot = await StateRootCalculator.ComputeStateRootAsync();

            var genesisHeader = GenesisHeaderBuilder.Build(_config, _config.PinnedFork, stateRoot);

            var genesisHash = CoreChain.BlockHashCalculator.ForFork(genesisHeader, _config.PinnedFork);
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
            _pendingBlockContext.SlotNumber = CarriesAmsterdamHeaderFieldsAt((long)nextBlockNumber, (ulong)timestamp)
                ? (ulong)nextBlockNumber : (ulong?)null;
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

        public bool CaptureWitness { get; set; }

        public IIncrementalStateRootCalculator StateRootCalculator { get; }

        public CoreChain.BlockExecutor Engine => _engine;

        public IBlockProducer BlockProducer => _blockProducer;

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

        private async Task<long> NextBlockTimestampAsync()
        {
            var clock = DateTime.UtcNow.ToUnixTimestamp() + _config.TimeOffset;
            var parent = await _blockStore.GetLatestAsync();
            return parent == null ? clock : Math.Max(clock, parent.Timestamp + 1);
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
            var timestamp = overrides.Timestamp ?? await NextBlockTimestampAsync();
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
                ParentBeaconBlockRoot = parentBeaconBlockRoot,
                // EIP-7843. DevChain has no consensus layer, so the producer is the
                // authority for the slot exactly as it is for the timestamp: one block
                // per slot, none missed, so the slot IS the block number. Supplied here
                // rather than defaulted inside BlockProducer, which must keep failing
                // loudly for a chain whose slot really does come from somewhere else.
                //
                // Gated on the same predicate as the block access list hash, because
                // EIP-7843 and EIP-7928 are emitted together or not at all: a Prague
                // header carrying a slot is a shape no fork produces, and
                // BlockHeaderCodecSelector refuses to encode it.
                SlotNumber = CarriesAmsterdamHeaderFieldsAt((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp)
                    ? (ulong)blockContext.BlockNumber : (ulong?)null,
                CaptureWitness = CaptureWitness
            };

            var result = await _blockProducer.ProduceBlockAsync(transactions, options);

            await RecordProducedBlockAsync(result);

            return result.BlockHash;
        }

        public async Task<BlockProductionResult> CommitBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions, bool fromMempool, BlockProductionOptions options)
        {
            await _mineLock.WaitAsync();
            try
            {
                options.SlotNumber ??= CarriesAmsterdamHeaderFieldsAt((long)_pendingBlockContext.BlockNumber, (ulong)options.Timestamp)
                    ? (ulong)_pendingBlockContext.BlockNumber : (ulong?)null;
                options.CaptureWitness = CaptureWitness;
                var result = await _blockProducer.ProduceBlockAsync(transactions.ToList(), options);
                if (fromMempool)
                {
                    lock (_pendingLock)
                    {
                        _pendingTransactionsList = new List<ISignedTransaction>();
                        _pendingHashes = new HashSet<string>();
                        _pendingNonces = new Dictionary<string, BigInteger>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    var included = new HashSet<string>(result.IncludedTransactions.Select(t => t.Hash.ToHex(true)));
                    lock (_pendingLock)
                    {
                        _pendingTransactionsList = _pendingTransactionsList.Where(t => !included.Contains(t.Hash.ToHex(true))).ToList();
                        _pendingHashes = new HashSet<string>(_pendingHashes.Where(h => !included.Contains(h)));
                    }
                }
                await RecordProducedBlockAsync(result);
                return result;
            }
            finally
            {
                _mineLock.Release();
            }
        }

        private async Task RecordProducedBlockAsync(BlockProductionResult result)
        {

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

            foreach (var dropped in result.DroppedTransactions)
            {
                _lastExecutionResults[dropped.Transaction.Hash.ToHex(true)] = new TransactionExecutionResult
                {
                    Transaction = dropped.Transaction,
                    TransactionHash = dropped.Transaction.Hash,
                    Success = false,
                    RevertReason = dropped.Reason
                };
            }

            LastBlockProductionResult = result;
            foreach (var deferred in result.DeferredTransactions)
                AddPendingTransaction(deferred);
            await InitializePendingBlockAsync();

        }

        public BlockProductionResult LastBlockProductionResult { get; private set; }

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

            var isContractCreation = tx.IsContractCreation();

            // EIP-2780 (Amsterdam+): the recipient/value component of the
            // intrinsic base depends on whether the transaction is a
            // self-transfer and whether it carries value. Ignored by
            // IntrinsicGasRules pre-Amsterdam.
            var isSelfTransfer = !isContractCreation && senderAddress.IsTheSameAddress(tx.GetReceiverAddress());
            var hasValue = !tx.GetValue().IsZero;

            var gasLimit = tx.GetGasLimit();
            var intrinsicGas = _config.GetHardforkConfig().IntrinsicGasRules
                .CalculateMinimumGasLimit(tx.GetData(), isContractCreation,
                    AccessListEntry.From(tx.GetAccessList()), isSelfTransfer, hasValue);
            if (gasLimit.ToBigInteger() < intrinsicGas)
            {
                result.Success = false;
                result.RevertReason = $"Intrinsic gas too low: have {gasLimit}, want {intrinsicGas}";
                return result;
            }

            var senderAccount = await _stateStore.GetAccountAsync(senderAddress);
            if (senderAccount == null)
            {
                senderAccount = new Account { Balance = 0, Nonce = 0 };
            }

            var nonce = tx.GetNonce();

            lock (_pendingLock)
            {
                var expectedNonce = _pendingNonces.TryGetValue(senderAddress, out var pendingNonce)
                    ? pendingNonce
                    : senderAccount.Nonce.ToBigInteger();

                var txNonce = nonce.ToBigInteger();

                if (txNonce < expectedNonce)
                {
                    result.Success = false;
                    result.RevertReason = $"nonce too low: have {txNonce}, want {expectedNonce}";
                    return result;
                }

                var maxCost = gasLimit * tx.GetMaxFeePerGas() + tx.GetValue();
                if (senderAccount.Balance < maxCost)
                {
                    result.Success = false;
                    result.RevertReason = $"Insufficient funds: have {senderAccount.Balance}, want {maxCost}";
                    return result;
                }

                if (txNonce == expectedNonce)
                    _pendingNonces[senderAddress] = (nonce + 1).ToBigInteger();
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
