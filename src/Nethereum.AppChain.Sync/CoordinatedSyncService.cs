using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;

namespace Nethereum.AppChain.Sync
{
    public class CoordinatedSyncService : IDisposable
    {
        private readonly CoordinatedSyncConfig _config;
        private readonly IBatchSyncService _batchSync;
        private readonly ILiveBlockSync _liveSync;
        private readonly IFinalityTracker _finalityTracker;
        private readonly IAnchorService _anchorService;
        private readonly IBatchStore _batchStore;

        private SyncMode _mode = SyncMode.Idle;
        private CancellationTokenSource? _syncCts;
        private Task? _syncLoopTask;
        private bool _disposed;

        public SyncMode Mode => _mode;
        public BigInteger FinalizedTip => _finalityTracker.LastFinalizedBlock;
        public BigInteger SoftTip => _finalityTracker.LastSoftBlock;
        public BigInteger AnchoredTip => _batchSync.AnchoredTip;

        public event EventHandler<CoordinatedSyncEventArgs>? SyncProgressChanged;
        public event EventHandler<BatchFinalizedEventArgs>? BatchFinalized;
        public event EventHandler<ReorgDetectedEventArgs>? ReorgDetected;

        public CoordinatedSyncService(
            CoordinatedSyncConfig config,
            IBatchSyncService batchSync,
            ILiveBlockSync liveSync,
            IFinalityTracker finalityTracker,
            IAnchorService anchorService,
            IBatchStore batchStore)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _batchSync = batchSync ?? throw new ArgumentNullException(nameof(batchSync));
            _liveSync = liveSync ?? throw new ArgumentNullException(nameof(liveSync));
            _finalityTracker = finalityTracker ?? throw new ArgumentNullException(nameof(finalityTracker));
            _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
            _batchStore = batchStore ?? throw new ArgumentNullException(nameof(batchStore));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_mode != SyncMode.Idle)
                return;

            await _batchSync.StartAsync(cancellationToken);
            await _liveSync.StartAsync(cancellationToken);

            _syncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _syncLoopTask = RunSyncLoopAsync(_syncCts.Token);
        }

        public async Task StopAsync()
        {
            _syncCts?.Cancel();

            if (_syncLoopTask != null)
            {
                try
                {
                    await _syncLoopTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await _liveSync.StopAsync();
            await _batchSync.StopAsync();

            _mode = SyncMode.Idle;
            _syncCts?.Dispose();
            _syncCts = null;
        }

        public async Task<CoordinatedSyncResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            var result = new CoordinatedSyncResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _mode = SyncMode.BatchSync;
                OnSyncProgressChanged("Starting batch sync...");

                var batchResult = await _batchSync.SyncToLatestAsync(cancellationToken);
                result.BatchesSynced = batchResult.BatchesSynced;
                result.FinalizedBlocks = batchResult.BlocksSynced;

                if (batchResult.Success && batchResult.EndBlock >= 0)
                {
                    await _finalityTracker.MarkRangeAsFinalizedAsync(0, batchResult.EndBlock);
                }

                _mode = SyncMode.LiveSync;
                OnSyncProgressChanged("Switching to live sync...");

                var liveResult = await _liveSync.SyncToLatestAsync(cancellationToken);
                result.SoftBlocks = liveResult.BlocksSynced;

                result.Success = batchResult.Success && liveResult.Success;
                result.FinalizedTip = _finalityTracker.LastFinalizedBlock;
                result.SoftTip = _finalityTracker.LastSoftBlock;

                _mode = SyncMode.Following;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Sync cancelled";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _mode = SyncMode.Error;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        public async Task<bool> CheckAndHandleNewAnchorAsync(CancellationToken cancellationToken = default)
        {
            var latestAnchoredBlock = await _anchorService.GetLatestAnchoredBlockAsync();

            if (latestAnchoredBlock <= _finalityTracker.LastFinalizedBlock)
                return false;

            var batchInfo = await _batchStore.GetBatchContainingBlockAsync(latestAnchoredBlock);
            if (batchInfo == null)
            {
                var batchResult = await _batchSync.SyncToBlockAsync(latestAnchoredBlock, cancellationToken);
                if (batchResult.Success)
                {
                    await _finalityTracker.MarkRangeAsFinalizedAsync(
                        _finalityTracker.LastFinalizedBlock + 1,
                        latestAnchoredBlock);

                    OnBatchFinalized(batchInfo, latestAnchoredBlock);
                    return true;
                }
            }
            else
            {
                await _finalityTracker.MarkRangeAsFinalizedAsync(batchInfo.FromBlock, batchInfo.ToBlock);
                OnBatchFinalized(batchInfo, latestAnchoredBlock);
                return true;
            }

            return false;
        }

        private async Task RunSyncLoopAsync(CancellationToken cancellationToken)
        {
            await SyncAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.AnchorCheckIntervalMs, cancellationToken);
                    await CheckAndHandleNewAnchorAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                }
            }
        }

        private void OnSyncProgressChanged(string message)
        {
            SyncProgressChanged?.Invoke(this, new CoordinatedSyncEventArgs
            {
                Mode = _mode,
                FinalizedTip = _finalityTracker.LastFinalizedBlock,
                SoftTip = _finalityTracker.LastSoftBlock,
                Message = message
            });
        }

        private void OnBatchFinalized(BatchInfo? batchInfo, BigInteger toBlock)
        {
            BatchFinalized?.Invoke(this, new BatchFinalizedEventArgs
            {
                BatchInfo = batchInfo,
                FinalizedToBlock = toBlock
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _syncCts?.Cancel();
            _syncCts?.Dispose();
        }
    }

    public enum SyncMode
    {
        Idle,
        BatchSync,
        LiveSync,
        Following,
        Error
    }

    public class CoordinatedSyncConfig
    {
        public int AnchorCheckIntervalMs { get; set; } = 60000;
        public bool AutoStart { get; set; } = true;

        public static CoordinatedSyncConfig Default => new()
        {
            AnchorCheckIntervalMs = 60000,
            AutoStart = true
        };
    }

    public class CoordinatedSyncResult
    {
        public bool Success { get; set; }
        public int BatchesSynced { get; set; }
        public int FinalizedBlocks { get; set; }
        public int SoftBlocks { get; set; }
        public BigInteger FinalizedTip { get; set; }
        public BigInteger SoftTip { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class CoordinatedSyncEventArgs : EventArgs
    {
        public SyncMode Mode { get; set; }
        public BigInteger FinalizedTip { get; set; }
        public BigInteger SoftTip { get; set; }
        public string Message { get; set; } = "";
    }

    public class BatchFinalizedEventArgs : EventArgs
    {
        public BatchInfo? BatchInfo { get; set; }
        public BigInteger FinalizedToBlock { get; set; }
    }

    public class ReorgDetectedEventArgs : EventArgs
    {
        public BigInteger ReorgFromBlock { get; set; }
        public BigInteger ReorgToBlock { get; set; }
        public string Reason { get; set; } = "";
    }
}
