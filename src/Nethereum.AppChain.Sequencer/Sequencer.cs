using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.AppChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.AppChain.Sequencer
{
    public class Sequencer : ISequencer, IAsyncDisposable
    {
        private readonly IAppChain _appChain;
        private readonly SequencerConfig _sequencerConfig;
        private readonly CoreChain.ITxPool _txPool;
        private readonly IBlockProducer _blockProducer;
        private readonly IPolicyEnforcer _policyEnforcer;
        private readonly ITransactionVerificationAndRecovery _txVerifier;
        private readonly IBlockProductionStrategy? _blockProductionStrategy;
        private readonly IBatchProducer? _batchProducer;
        private readonly IBatchStore? _batchStore;
        private readonly IMessageQueue? _messageQueue;
        private readonly IMessageProcessor? _messageProcessor;
        private readonly ILogger<Sequencer>? _logger;
        private readonly string _nodeId;

        private CancellationTokenSource? _cts;
        private Task? _blockProductionTask;
        private bool _running;
        private int _consecutiveFailures;
        private readonly SemaphoreSlim _submitLock = new SemaphoreSlim(1, 1);

        public SequencerConfig Config => _sequencerConfig;
        public IAppChain AppChain => _appChain;
        public CoreChain.ITxPool TxPool => _txPool;
        public IPolicyEnforcer PolicyEnforcer => _policyEnforcer;
        public IBlockProductionStrategy? BlockProductionStrategy => _blockProductionStrategy;
        public IBatchProducer? BatchProducer => _batchProducer;

        public event EventHandler<BlockProductionResult>? BlockProduced;
        public event EventHandler<BatchProductionResult>? BatchProduced;

        public Sequencer(
            IAppChain appChain,
            SequencerConfig config,
            CoreChain.ITxPool? txPool = null,
            IBlockProducer? blockProducer = null,
            IPolicyEnforcer? policyEnforcer = null,
            IBatchStore? batchStore = null,
            IBatchProducer? batchProducer = null,
            IBlockProductionStrategy? blockProductionStrategy = null,
            IMessageQueue? messageQueue = null,
            IMessageProcessor? messageProcessor = null,
            ILogger<Sequencer>? logger = null,
            string? nodeId = null,
            CoreChain.IncrementalStateRootCalculator? stateRootCalculator = null)
        {
            _appChain = appChain ?? throw new ArgumentNullException(nameof(appChain));
            _sequencerConfig = config ?? throw new ArgumentNullException(nameof(config));
            _txVerifier = new TransactionVerificationAndRecoveryImp();
            _blockProductionStrategy = blockProductionStrategy;
            _messageQueue = messageQueue;
            _messageProcessor = messageProcessor;
            _logger = logger;
            _nodeId = nodeId ?? $"Node-{appChain.Config.ChainId}";

            _txPool = txPool ?? new CoreChain.TxPool(maxPoolSize: _sequencerConfig.MaxPoolSize, maxTxsPerSender: _sequencerConfig.MaxTxsPerSender);
            _policyEnforcer = policyEnforcer ?? new PolicyEnforcer(config.Policy, appChain);

            if (blockProducer == null)
            {
                var transactionProcessor = CreateTransactionProcessor();
                _blockProducer = new BlockProducer(appChain, transactionProcessor, blockProductionStrategy, stateRootCalculator);
            }
            else
            {
                _blockProducer = blockProducer;
            }

            if (config.BatchProduction.Enabled)
            {
                _batchStore = batchStore ?? new InMemoryBatchStore();
                _batchProducer = batchProducer ?? new SequencerBatchProducer(
                    appChain.Blocks,
                    appChain.Transactions,
                    appChain.Receipts,
                    _batchStore,
                    config.BatchProduction,
                    appChain.Config.ChainId);
            }
        }

        private TransactionProcessor CreateTransactionProcessor()
        {
            return new TransactionProcessor(
                _appChain.State,
                _appChain.Blocks,
                _appChain.Config,
                _txVerifier,
                _appChain.Config.GetHardforkConfig());
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_running)
                return;

            await _appChain.InitializeAsync();

            if (_batchProducer is SequencerBatchProducer sbp)
            {
                await sbp.InitializeAsync();
            }

            _running = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_sequencerConfig.BlockTimeMs > 0)
            {
                _blockProductionTask = RunBlockProductionLoopAsync(_cts.Token);
            }
        }

        public async Task StopAsync()
        {
            if (!_running)
                return;

            _running = false;
            _cts?.Cancel();

            if (_blockProductionTask != null)
            {
                try
                {
                    await _blockProductionTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            _cts?.Dispose();
            _cts = null;
        }

        public async Task<byte[]> SubmitTransactionAsync(ISignedTransaction transaction)
        {
            var validation = await _policyEnforcer.ValidateTransactionAsync(transaction);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Transaction rejected: {validation.ErrorMessage}");
            }

            var senderAddress = await ValidateTransactionAsync(transaction);

            if (_sequencerConfig.BlockProductionMode == BlockProductionMode.OnDemand)
            {
                await _submitLock.WaitAsync();
                try
                {
                    var txHash = await _txPool.AddAsync(transaction);
                    TrackNonceAfterAdd(transaction, senderAddress);
                    await ProduceBlockAsync();
                    return txHash;
                }
                finally
                {
                    _submitLock.Release();
                }
            }

            var hash = await _txPool.AddAsync(transaction);
            TrackNonceAfterAdd(transaction, senderAddress);
            return hash;
        }

        private void TrackNonceAfterAdd(ISignedTransaction transaction, string senderAddress)
        {
            var txData = TransactionProcessor.GetTransactionData(transaction);
            _txPool.TrackPendingNonce(senderAddress, txData.Nonce);
            _txPool.IncrementSenderTxCount(senderAddress);
        }

        private async Task<string> ValidateTransactionAsync(ISignedTransaction transaction)
        {
            var senderAddress = _txVerifier.GetSenderAddress(transaction);
            if (string.IsNullOrEmpty(senderAddress))
            {
                throw new InvalidOperationException("invalid sender: unable to recover sender address");
            }

            var txData = TransactionProcessor.GetTransactionData(transaction);
            var isContractCreation = transaction.IsContractCreation();

            if (_txPool.MaxTxsPerSender > 0)
            {
                var senderCount = _txPool.GetSenderTxCount(senderAddress);
                if (senderCount >= _txPool.MaxTxsPerSender)
                {
                    throw new InvalidOperationException(
                        $"tx pool per-sender limit reached: address {senderAddress} has {senderCount} pending txs (max {_txPool.MaxTxsPerSender})");
                }
            }

            var intrinsicGas = TransactionProcessor.CalculateIntrinsicGas(txData.Data, isContractCreation);
            if (txData.GasLimit.ToBigInteger() < intrinsicGas)
            {
                throw new InvalidOperationException(
                    $"intrinsic gas too low: have {txData.GasLimit}, want {intrinsicGas}");
            }

            var senderAccount = await _appChain.State.GetAccountAsync(senderAddress);
            var confirmedNonce = senderAccount?.Nonce.ToBigInteger() ?? BigInteger.Zero;

            var expectedNonce = await _txPool.GetPendingNonceAsync(senderAddress, confirmedNonce);
            var txNonceBig = txData.Nonce.ToBigInteger();

            if (txNonceBig < expectedNonce)
            {
                throw new InvalidOperationException(
                    $"nonce too low: address {senderAddress}, tx: {txData.Nonce} state: {expectedNonce}");
            }

            if (txNonceBig > expectedNonce)
            {
                throw new InvalidOperationException(
                    $"nonce too high: address {senderAddress}, tx: {txData.Nonce} state: {expectedNonce}");
            }

            var balance = senderAccount?.Balance ?? BigInteger.Zero;
            var maxCost = txData.GasLimit * txData.GasPrice + txData.Value;
            if (balance < maxCost)
            {
                throw new InvalidOperationException(
                    $"insufficient funds for gas * price + value: address {senderAddress} have {balance} want {maxCost}");
            }

            return senderAddress;
        }

        public async Task<byte[]> ProduceBlockAsync()
        {
            var sw = Stopwatch.StartNew();

            List<MessageInfo>? pendingMessages = null;
            if (_messageQueue != null && _messageQueue.Count > 0)
            {
                pendingMessages = _messageQueue.DrainBatch(_sequencerConfig.MaxMessagesPerBlock);
            }

            var pending = await _txPool.GetPendingAsync(_sequencerConfig.MaxTransactionsPerBlock);

            if (pending.Count > 0)
            {
                var claimedHashes = pending.Select(tx => tx.Hash).ToList();
                await _txPool.RemoveBatchAsync(claimedHashes);
            }

            BlockProductionResult result;
            try
            {
                result = await _blockProducer.ProduceBlockAsync(pending);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[{NodeId}] Block production failed, restoring {TxCount} transactions to pool", _nodeId, pending.Count);
                foreach (var tx in pending)
                {
                    try { await _txPool.AddAsync(tx); } catch { }
                }
                if (pendingMessages != null && pendingMessages.Count > 0 && _messageQueue != null)
                {
                    _messageQueue.EnqueueRange(pendingMessages);
                    _logger?.LogWarning("[{NodeId}] Re-enqueued {MsgCount} messages after block production failure", _nodeId, pendingMessages.Count);
                }
                throw;
            }

            if (_messageProcessor != null && pendingMessages != null && pendingMessages.Count > 0)
            {
                try
                {
                    var messageBatchResult = await _messageProcessor.ProcessBatchAsync(pendingMessages);
                    result.MessageBatchResult = messageBatchResult;

                    _logger?.LogInformation(
                        "[{NodeId}][Block {BlockNumber}] Processed {MsgCount} messages ({Failed} failed)",
                        _nodeId, result.Header.BlockNumber, messageBatchResult.ProcessedCount, messageBatchResult.FailedCount);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[{NodeId}] Message processing failed for block {BlockNumber}, re-enqueuing {MsgCount} messages",
                        _nodeId, result.Header.BlockNumber, pendingMessages.Count);
                    _messageQueue?.EnqueueRange(pendingMessages);
                }
            }

            sw.Stop();

            var txCount = result.TransactionResults.Count;
            var gasUsed = result.Header.GasUsed;
            var blockNumber = result.Header.BlockNumber;
            var coinbase = result.Header.Coinbase ?? "unknown";

            // Verbose logging for each block
            _logger?.LogInformation(
                "[{NodeId}][Block {BlockNumber}] Produced by {Coinbase} | {TxCount} TXs | {GasUsed:N0} gas | {TimeMs:F1}ms",
                _nodeId, blockNumber, coinbase.Length > 10 ? coinbase.Substring(0, 10) + "..." : coinbase,
                txCount, gasUsed, sw.ElapsedMilliseconds);

            // Log each TX result for traceability
            var successCount = 0;
            var revertedCount = 0;
            foreach (var txResult in result.TransactionResults)
            {
                if (txResult.Success)
                {
                    successCount++;
                    _logger?.LogDebug(
                        "[{NodeId}] TX {TxHash} INCLUDED Block {BlockNumber} | gas={GasUsed} | SUCCESS",
                        _nodeId, txResult.TxHash?.ToHex(true).Substring(0, 10) ?? "null", blockNumber, txResult.GasUsed);
                }
                else
                {
                    revertedCount++;
                    _logger?.LogWarning(
                        "[{NodeId}] TX {TxHash} REVERTED Block {BlockNumber} | gas={GasUsed} | reason={Reason}",
                        _nodeId, txResult.TxHash?.ToHex(true).Substring(0, 10) ?? "null", blockNumber,
                        txResult.GasUsed, txResult.ErrorMessage ?? "unknown");
                }
            }

            // Summary line for quick scanning
            if (revertedCount > 0)
            {
                _logger?.LogWarning(
                    "[{NodeId}][Block {BlockNumber}] Summary: {Success} success, {Reverted} reverted",
                    _nodeId, blockNumber, successCount, revertedCount);
            }

            BlockProduced?.Invoke(this, result);

            _txPool.ResetPendingNonces();

            if (_batchProducer != null && _batchProducer.IsBatchDue(result.Header.BlockNumber))
            {
                var batchResult = await _batchProducer.ProduceBatchIfDueAsync(result.Header.BlockNumber, _cts?.Token ?? default);
                if (batchResult.Success)
                {
                    BatchProduced?.Invoke(this, batchResult);
                }
            }

            return result.BlockHash;
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            return await _appChain.GetBlockNumberAsync();
        }

        public async Task<BlockHeader?> GetLatestBlockAsync()
        {
            return await _appChain.GetLatestBlockAsync();
        }

        private async Task RunBlockProductionLoopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Background block production loop started. BlockTimeMs={BlockTimeMs}, Mode={Mode}, AllowEmptyBlocks={AllowEmpty}",
                _sequencerConfig.BlockTimeMs, _sequencerConfig.BlockProductionMode, _sequencerConfig.AllowEmptyBlocks);

            var targetIntervalMs = _sequencerConfig.BlockTimeMs;

            while (!cancellationToken.IsCancellationRequested)
            {
                var iterationStart = Stopwatch.StartNew();

                try
                {
                    var currentHeight = await _appChain.GetBlockNumberAsync();
                    var nextBlockNumber = (long)currentHeight + 1;

                    // Check if we can produce (strategy may enforce turn-based production)
                    if (_blockProductionStrategy != null && !_blockProductionStrategy.CanProduceBlock(nextBlockNumber))
                    {
                        _logger?.LogDebug("CanProduceBlock returned false for block {BlockNumber}, waiting for next interval", nextBlockNumber);
                        await DelayRemainingTimeAsync(iterationStart, targetIntervalMs, cancellationToken);
                        continue;
                    }

                    // Get signing delay (e.g., Clique wiggle time for out-of-turn)
                    if (_blockProductionStrategy != null)
                    {
                        var signingDelay = await _blockProductionStrategy.GetSigningDelayAsync(nextBlockNumber, cancellationToken);
                        if (signingDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(signingDelay, cancellationToken);
                        }

                        // Re-check block number after delay (someone else may have produced)
                        var newHeight = await _appChain.GetBlockNumberAsync();
                        if (newHeight >= nextBlockNumber)
                        {
                            continue;
                        }
                    }

                    var pendingCount = await _txPool.GetPendingCountAsync();
                    if (pendingCount > 0 || _sequencerConfig.AllowEmptyBlocks)
                    {
                        await ProduceBlockAsync();
                        _consecutiveFailures = 0;
                    }

                    // Wait remaining time to hit target interval (accounts for production time)
                    await DelayRemainingTimeAsync(iterationStart, targetIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    _logger?.LogError(ex, "Block production failed (attempt {Failures})", _consecutiveFailures);

                    if (_consecutiveFailures >= 10)
                    {
                        _logger?.LogCritical("Block production circuit breaker triggered after {Failures} consecutive failures", _consecutiveFailures);
                        await Task.Delay(30000, cancellationToken);
                        _consecutiveFailures = 0;
                    }
                    else
                    {
                        await Task.Delay(Math.Min(1000 * _consecutiveFailures, 10000), cancellationToken);
                    }
                }
            }
        }

        private static async Task DelayRemainingTimeAsync(Stopwatch elapsed, int targetMs, CancellationToken ct)
        {
            var remainingMs = targetMs - (int)elapsed.ElapsedMilliseconds;
            if (remainingMs > 0)
            {
                await Task.Delay(remainingMs, ct);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_running)
            {
                await StopAsync();
            }
            _submitLock.Dispose();
        }
    }
}
