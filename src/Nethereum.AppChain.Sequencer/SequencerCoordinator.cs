using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sync;

namespace Nethereum.AppChain.Sequencer
{
    public class SequencerCoordinator : ISequencerCoordinator
    {
        private readonly ISequencer _sequencer;
        private readonly ILiveBlockSync? _syncService;
        private readonly IPeerManager? _peerManager;
        private readonly SequencerCoordinatorConfig _config;
        private readonly ILogger<SequencerCoordinator>? _logger;

        private CancellationTokenSource? _cts;
        private Task? _coordinatorTask;
        private SequencerMode _mode = SequencerMode.Stopped;
        private BigInteger _syncTargetBlock = -1;

        public SequencerMode Mode => _mode;
        public BigInteger SyncTarget => _syncTargetBlock;
        public bool IsSyncing => _mode == SequencerMode.Syncing;
        public bool IsProducing => _mode == SequencerMode.Producing;

        public event EventHandler<SequencerModeChangedEventArgs>? ModeChanged;
        public event EventHandler<SyncProgressEventArgs>? SyncProgress;

        public async Task<BigInteger> GetLocalHeightAsync()
        {
            return await _sequencer.AppChain.Blocks.GetHeightAsync();
        }

        public SequencerCoordinator(
            ISequencer sequencer,
            ILiveBlockSync? syncService = null,
            IPeerManager? peerManager = null,
            SequencerCoordinatorConfig? config = null,
            ILogger<SequencerCoordinator>? logger = null)
        {
            _sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
            _syncService = syncService;
            _peerManager = peerManager;
            _config = config ?? SequencerCoordinatorConfig.Default;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_mode != SequencerMode.Stopped)
                return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_syncService != null && _peerManager != null && _peerManager.Peers.Count > 0)
            {
                SetMode(SequencerMode.Syncing);
                _coordinatorTask = RunCoordinatorLoopAsync(_cts.Token);
            }
            else
            {
                _logger?.LogInformation("No sync service or peers configured, starting production immediately");
                await StartProducingAsync(_cts.Token);
            }
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();

            if (_coordinatorTask != null)
            {
                try
                {
                    await _coordinatorTask;
                }
                catch (OperationCanceledException) { }
            }

            await _sequencer.StopAsync();

            if (_syncService != null)
            {
                await _syncService.StopAsync();
            }

            SetMode(SequencerMode.Stopped);
            _cts?.Dispose();
            _cts = null;
        }

        private async Task RunCoordinatorLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Phase 1: Initial sync
                await SyncToLatestAsync(cancellationToken);

                // Phase 2: Start producing
                await StartProducingAsync(cancellationToken);

                // Phase 3: Monitor and re-sync if needed
                await MonitorAndResyncLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Coordinator loop failed");
                throw;
            }
        }

        private async Task SyncToLatestAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting initial sync...");

            // Get remote tip from peers
            if (_peerManager != null)
            {
                await _peerManager.CheckAllPeersAsync(cancellationToken);
            }

            var remoteTip = await _syncService!.GetRemoteTipAsync(cancellationToken);
            var localHeight = await _sequencer.AppChain.Blocks.GetHeightAsync();

            _syncTargetBlock = remoteTip;

            _logger?.LogInformation("Local height: {Local}, Remote tip: {Remote}, Need to sync: {Count} blocks",
                localHeight, remoteTip, remoteTip - localHeight);

            if (remoteTip <= localHeight)
            {
                _logger?.LogInformation("Already synced to latest block");
                return;
            }

            // Start sync service
            await _syncService.StartAsync(cancellationToken);

            // Wait for sync to complete
            var lastProgress = localHeight;
            while (!cancellationToken.IsCancellationRequested)
            {
                var currentHeight = await _sequencer.AppChain.Blocks.GetHeightAsync();
                var currentRemoteTip = await _syncService.GetRemoteTipAsync(cancellationToken);

                // Update target if remote moved
                if (currentRemoteTip > _syncTargetBlock)
                {
                    _syncTargetBlock = currentRemoteTip;
                }

                // Report progress
                if (currentHeight > lastProgress)
                {
                    var progress = (double)(currentHeight - localHeight) / (double)(_syncTargetBlock - localHeight) * 100;
                    SyncProgress?.Invoke(this, new SyncProgressEventArgs
                    {
                        CurrentBlock = currentHeight,
                        TargetBlock = _syncTargetBlock,
                        ProgressPercent = Math.Min(progress, 100)
                    });

                    _logger?.LogInformation("Sync progress: {Current}/{Target} ({Progress:F1}%)",
                        currentHeight, _syncTargetBlock, progress);

                    lastProgress = currentHeight;
                }

                // Check if synced (within threshold)
                var behind = _syncTargetBlock - currentHeight;
                if (behind <= _config.SyncThresholdBlocks)
                {
                    _logger?.LogInformation("Sync complete. Local: {Local}, Target: {Target}",
                        currentHeight, _syncTargetBlock);
                    break;
                }

                await Task.Delay(_config.SyncCheckIntervalMs, cancellationToken);
            }
        }

        private async Task StartProducingAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting block production...");
            SetMode(SequencerMode.Producing);
            await _sequencer.StartAsync(cancellationToken);
        }

        private async Task MonitorAndResyncLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_config.MonitorIntervalMs, cancellationToken);

                if (_syncService == null || _peerManager == null)
                    continue;

                // Check if we've fallen behind
                var localHeight = await _sequencer.AppChain.Blocks.GetHeightAsync();
                var remoteTip = await _syncService.GetRemoteTipAsync(cancellationToken);
                var behind = remoteTip - localHeight;

                if (behind > _config.ResyncThresholdBlocks)
                {
                    _logger?.LogWarning("Fallen behind by {Behind} blocks. Local: {Local}, Remote: {Remote}. Pausing production to sync.",
                        behind, localHeight, remoteTip);

                    // Pause production
                    await _sequencer.StopAsync();
                    SetMode(SequencerMode.Syncing);

                    // Re-sync
                    await SyncToLatestAsync(cancellationToken);

                    // Resume production
                    await StartProducingAsync(cancellationToken);
                }
            }
        }

        private void SetMode(SequencerMode newMode)
        {
            if (_mode == newMode) return;

            var oldMode = _mode;
            _mode = newMode;

            ModeChanged?.Invoke(this, new SequencerModeChangedEventArgs
            {
                PreviousMode = oldMode,
                CurrentMode = newMode
            });
        }
    }

    public interface ISequencerCoordinator
    {
        SequencerMode Mode { get; }
        Task<BigInteger> GetLocalHeightAsync();
        BigInteger SyncTarget { get; }
        bool IsSyncing { get; }
        bool IsProducing { get; }

        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();

        event EventHandler<SequencerModeChangedEventArgs>? ModeChanged;
        event EventHandler<SyncProgressEventArgs>? SyncProgress;
    }

    public enum SequencerMode
    {
        Stopped,
        Syncing,
        Producing
    }

    public class SequencerCoordinatorConfig
    {
        public int SyncCheckIntervalMs { get; set; } = 1000;
        public int MonitorIntervalMs { get; set; } = 5000;
        public int SyncThresholdBlocks { get; set; } = 2;
        public int ResyncThresholdBlocks { get; set; } = 10;

        public static SequencerCoordinatorConfig Default => new();
    }

    public class SequencerModeChangedEventArgs : EventArgs
    {
        public SequencerMode PreviousMode { get; set; }
        public SequencerMode CurrentMode { get; set; }
    }

    public class SyncProgressEventArgs : EventArgs
    {
        public BigInteger CurrentBlock { get; set; }
        public BigInteger TargetBlock { get; set; }
        public double ProgressPercent { get; set; }
    }
}
