using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class MultiPeerSyncService : ILiveBlockSync
    {
        private readonly MultiPeerSyncConfig _config;
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IFinalityTracker _finalityTracker;
        private readonly IPeerManager _peerManager;
        private readonly IBlockReExecutor? _blockReExecutor;
        private readonly ILogger<MultiPeerSyncService>? _logger;

        private LiveSyncState _state = LiveSyncState.Idle;
        private BigInteger _localTip = -1;
        private BigInteger _remoteTip = -1;
        private CancellationTokenSource? _syncCts;
        private Task? _syncTask;
        private readonly Sha3Keccack _keccak = new();
        private readonly SemaphoreSlim _syncLock = new(1, 1);

        public BigInteger LocalTip => _localTip;
        public BigInteger RemoteTip => _remoteTip;
        public LiveSyncState State => _state;
        public string? CurrentPeerUrl { get; private set; }

        public event EventHandler<LiveBlockImportedEventArgs>? BlockImported;
        public event EventHandler<LiveSyncErrorEventArgs>? Error;
        public event EventHandler<PeerSwitchedEventArgs>? PeerSwitched;
        public event EventHandler<StateRootMismatchEventArgs>? StateRootMismatch;

        public MultiPeerSyncService(
            MultiPeerSyncConfig config,
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IFinalityTracker finalityTracker,
            IPeerManager peerManager,
            IBlockReExecutor? blockReExecutor = null,
            ILogger<MultiPeerSyncService>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _finalityTracker = finalityTracker ?? throw new ArgumentNullException(nameof(finalityTracker));
            _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            _blockReExecutor = blockReExecutor;
            _logger = logger;

            if (_blockReExecutor != null)
            {
                _logger?.LogInformation("MultiPeerSyncService initialized with transaction re-execution enabled");
            }
            else
            {
                _logger?.LogWarning("MultiPeerSyncService initialized WITHOUT transaction re-execution - follower nodes will not have state");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_state != LiveSyncState.Idle)
                return;

            _localTip = await _blockStore.GetHeightAsync();

            await _peerManager.StartHealthCheckAsync(cancellationToken);
            await _peerManager.CheckAllPeersAsync(cancellationToken);

            _remoteTip = await GetRemoteTipAsync(cancellationToken);

            _syncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_config.AutoFollow)
            {
                _syncTask = RunFollowLoopAsync(_syncCts.Token);
            }
        }

        public async Task StopAsync()
        {
            _syncCts?.Cancel();

            if (_syncTask != null)
            {
                try
                {
                    await _syncTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await _peerManager.StopHealthCheckAsync();

            _state = LiveSyncState.Idle;
            _syncCts?.Dispose();
            _syncCts = null;
        }

        public async Task<LiveSyncResult> SyncToLatestAsync(CancellationToken cancellationToken = default)
        {
            _remoteTip = await GetRemoteTipAsync(cancellationToken);
            return await SyncToBlockAsync(_remoteTip, cancellationToken);
        }

        public async Task<LiveSyncResult> SyncToBlockAsync(BigInteger targetBlock, CancellationToken cancellationToken = default)
        {
            await _syncLock.WaitAsync(cancellationToken);
            try
            {
                return await SyncToBlockInternalAsync(targetBlock, cancellationToken);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task<LiveSyncResult> SyncToBlockInternalAsync(BigInteger targetBlock, CancellationToken cancellationToken)
        {
            var result = new LiveSyncResult
            {
                StartBlock = _localTip + 1
            };
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _state = LiveSyncState.Syncing;
                _localTip = await _blockStore.GetHeightAsync();

                if (_localTip >= targetBlock)
                {
                    result.Success = true;
                    result.EndBlock = _localTip;
                    return result;
                }

                var blocksSynced = 0;
                var currentBlock = _localTip + 1;

                while (currentBlock <= targetBlock)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var blockData = await FetchBlockWithFailoverAsync(currentBlock, cancellationToken);
                    if (blockData == null)
                    {
                        break;
                    }

                    await ImportBlockAsync(blockData, cancellationToken);
                    blocksSynced++;
                    _localTip = currentBlock;

                    OnBlockImported(blockData);

                    currentBlock++;
                }

                result.Success = true;
                result.EndBlock = _localTip;
                result.BlocksSynced = blocksSynced;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Sync cancelled";
            }
            catch (Exception ex)
            {
                OnError($"Sync failed: {ex.Message}", ex, false);
                result.ErrorMessage = ex.Message;
                _state = LiveSyncState.Error;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                if (_state == LiveSyncState.Syncing)
                {
                    _state = LiveSyncState.Idle;
                }
            }

            return result;
        }

        public async Task<BigInteger> GetRemoteTipAsync(CancellationToken cancellationToken = default)
        {
            var peers = _peerManager.Peers;
            BigInteger highestTip = -1;

            foreach (var peer in peers)
            {
                if (!peer.IsHealthy) continue;

                try
                {
                    var tip = await peer.Client.GetBlockNumberAsync(cancellationToken);
                    if (tip > highestTip)
                    {
                        highestTip = tip;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to get block number from peer {PeerUrl}", peer.Url);
                }
            }

            if (highestTip < 0)
            {
                var client = await _peerManager.GetHealthyClientAsync(cancellationToken);
                if (client != null)
                {
                    highestTip = await client.GetBlockNumberAsync(cancellationToken);
                }
            }

            _remoteTip = highestTip;
            return _remoteTip;
        }

        public Task<LiveBlockData?> FetchBlockAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            return FetchBlockWithFailoverAsync(blockNumber, cancellationToken);
        }

        private async Task<LiveBlockData?> FetchBlockWithFailoverAsync(BigInteger blockNumber, CancellationToken cancellationToken)
        {
            var attemptedPeers = new System.Collections.Generic.HashSet<string>();
            var maxAttempts = _config.MaxPeerRetries;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var peer = _peerManager.GetBestPeer();

                if (peer == null)
                {
                    await _peerManager.CheckAllPeersAsync(cancellationToken);
                    peer = _peerManager.GetBestPeer();
                }

                if (peer == null)
                {
                    OnError("No healthy peers available", null, true);
                    return null;
                }

                if (attemptedPeers.Contains(peer.Url))
                {
                    var nextPeer = GetNextPeer(attemptedPeers);
                    if (nextPeer == null)
                    {
                        OnError("All peers failed", null, false);
                        return null;
                    }
                    peer = nextPeer;
                }

                attemptedPeers.Add(peer.Url);

                if (CurrentPeerUrl != peer.Url)
                {
                    var previousPeer = CurrentPeerUrl;
                    CurrentPeerUrl = peer.Url;
                    PeerSwitched?.Invoke(this, new PeerSwitchedEventArgs
                    {
                        PreviousPeerUrl = previousPeer,
                        NewPeerUrl = peer.Url,
                        Reason = previousPeer == null ? "Initial connection" : "Failover"
                    });
                }

                try
                {
                    var blockData = await peer.Client.GetBlockWithReceiptsAsync(blockNumber, cancellationToken);
                    if (blockData != null)
                    {
                        return blockData;
                    }
                }
                catch (Exception ex)
                {
                    OnError($"Peer {peer.Url} failed for block {blockNumber}: {ex.Message}", ex, true);
                    peer.IsHealthy = false;
                    peer.FailureCount++;
                    peer.LastError = ex.Message;
                }
            }

            return null;
        }

        private SyncPeer? GetNextPeer(System.Collections.Generic.HashSet<string> excludeUrls)
        {
            foreach (var peer in _peerManager.Peers)
            {
                if (peer.IsHealthy && !excludeUrls.Contains(peer.Url))
                {
                    return peer;
                }
            }
            return null;
        }

        private async Task ImportBlockAsync(LiveBlockData blockData, CancellationToken cancellationToken)
        {
            var computedHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(blockData.Header));
            var blockHash = blockData.BlockHash;
            if (blockHash == null || blockHash.Length == 0)
            {
                blockHash = computedHash;
            }
            else if (!ByteUtil.AreEqual(blockHash, computedHash))
            {
                throw new InvalidBlockException(
                    $"Block {blockData.Header.BlockNumber}: hash mismatch — " +
                    $"peer sent {blockHash.ToHex(true)}, computed {computedHash.ToHex(true)}");
            }

            if (blockData.Header.BlockNumber > 0 && blockData.Header.ParentHash != null)
            {
                var parentHash = await _blockStore.GetHashByNumberAsync(blockData.Header.BlockNumber - 1);
                if (parentHash != null && !ByteUtil.AreEqual(blockData.Header.ParentHash, parentHash))
                {
                    throw new InvalidBlockException(
                        $"Block {blockData.Header.BlockNumber}: parent hash mismatch — " +
                        $"header says {blockData.Header.ParentHash.ToHex(true)}, " +
                        $"local block {blockData.Header.BlockNumber - 1} is {parentHash.ToHex(true)}");
                }
            }

            // Re-execute transactions to build state (if executor is configured)
            // Note: We also validate genesis blocks (0 transactions) to ensure state root matches
            if (_blockReExecutor != null)
            {
                var reExecResult = await _blockReExecutor.ReExecuteBlockAsync(
                    blockData.Header,
                    blockData.Transactions,
                    cancellationToken);

                if (!reExecResult.Success)
                {
                    _logger?.LogError(
                        "Block {BlockNumber} re-execution failed: {Error}",
                        blockData.Header.BlockNumber, reExecResult.ErrorMessage);

                    if (!reExecResult.StateRootMatches && _config.RejectOnStateRootMismatch)
                    {
                        OnStateRootMismatch(blockData.Header, reExecResult);
                        throw new InvalidBlockException(
                            $"State root mismatch for block {blockData.Header.BlockNumber}: " +
                            $"expected {reExecResult.ExpectedStateRoot?.ToHex(true)}, " +
                            $"got {reExecResult.ComputedStateRoot?.ToHex(true)}");
                    }
                }
                else
                {
                    _logger?.LogDebug(
                        "Block {BlockNumber} re-executed: {TxCount} transactions, state root validated",
                        blockData.Header.BlockNumber, reExecResult.TransactionsExecuted);
                }
            }

            // Save block header
            await _blockStore.SaveAsync(blockData.Header, blockHash);

            // Build a map of transaction hash to receipt for reliable matching
            var receiptsByTxHash = new System.Collections.Generic.Dictionary<string, (Receipt receipt, int index)>();
            for (int i = 0; i < blockData.Receipts.Count; i++)
            {
                var receipt = blockData.Receipts[i];
                if (i < blockData.Transactions.Count)
                {
                    var txHashKey = blockData.Transactions[i].Hash.ToHex();
                    receiptsByTxHash[txHashKey] = (receipt, i);
                }
            }

            // Save transactions and receipts
            for (int i = 0; i < blockData.Transactions.Count; i++)
            {
                var tx = blockData.Transactions[i];
                await _transactionStore.SaveAsync(tx, blockHash, i, blockData.Header.BlockNumber);

                var txHashKey = tx.Hash.ToHex();
                if (receiptsByTxHash.TryGetValue(txHashKey, out var receiptInfo))
                {
                    var receipt = receiptInfo.receipt;
                    var receiptIndex = receiptInfo.index;
                    var gasUsed = receipt.CumulativeGasUsed;
                    if (receiptIndex > 0 && receiptIndex < blockData.Receipts.Count)
                    {
                        gasUsed -= blockData.Receipts[receiptIndex - 1].CumulativeGasUsed;
                    }

                    await _receiptStore.SaveAsync(
                        receipt,
                        tx.Hash,
                        blockHash,
                        blockData.Header.BlockNumber,
                        i,
                        gasUsed,
                        null,
                        0);

                    if (receipt.Logs != null && receipt.Logs.Count > 0)
                    {
                        await _logStore.SaveLogsAsync(
                            receipt.Logs,
                            tx.Hash,
                            blockHash,
                            blockData.Header.BlockNumber,
                            i);
                    }
                }
            }

            await _finalityTracker.MarkAsSoftAsync(blockData.Header.BlockNumber);
        }

        private void OnStateRootMismatch(BlockHeader header, BlockReExecutionResult result)
        {
            StateRootMismatch?.Invoke(this, new StateRootMismatchEventArgs
            {
                BlockNumber = header.BlockNumber,
                ExpectedStateRoot = result.ExpectedStateRoot,
                ComputedStateRoot = result.ComputedStateRoot
            });
        }

        private async Task RunFollowLoopAsync(CancellationToken cancellationToken)
        {
            _state = LiveSyncState.FollowingHead;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.PollIntervalMs, cancellationToken);

                    var remoteTip = await GetRemoteTipAsync(cancellationToken);

                    if (remoteTip > _localTip)
                    {
                        await SyncToBlockAsync(remoteTip, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError($"Follow loop error: {ex.Message}", ex, true);
                    await Task.Delay(_config.ErrorRetryDelayMs, cancellationToken);
                }
            }
        }

        private void OnBlockImported(LiveBlockData blockData)
        {
            BlockImported?.Invoke(this, new LiveBlockImportedEventArgs
            {
                BlockNumber = blockData.Header.BlockNumber,
                BlockHash = blockData.BlockHash,
                IsSoft = blockData.IsSoft,
                TransactionCount = blockData.Transactions.Count
            });
        }

        private void OnError(string message, Exception? ex, bool recoverable)
        {
            Error?.Invoke(this, new LiveSyncErrorEventArgs
            {
                Message = message,
                Exception = ex,
                Recoverable = recoverable
            });
        }
    }

    public class MultiPeerSyncConfig
    {
        public int PollIntervalMs { get; set; } = 1000;
        public int ErrorRetryDelayMs { get; set; } = 5000;
        public bool AutoFollow { get; set; } = true;
        public int MaxPeerRetries { get; set; } = 3;
        public bool RejectOnStateRootMismatch { get; set; } = true;

        public static MultiPeerSyncConfig Default => new();
    }

    public class PeerSwitchedEventArgs : EventArgs
    {
        public string? PreviousPeerUrl { get; set; }
        public string? NewPeerUrl { get; set; }
        public string? Reason { get; set; }
    }
}
