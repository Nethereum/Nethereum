using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IBatchSyncService
    {
        BatchSyncState State { get; }

        BigInteger LocalTip { get; }

        BigInteger AnchoredTip { get; }

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync();

        Task<BatchSyncResult> SyncToLatestAsync(CancellationToken cancellationToken = default);

        Task<BatchSyncResult> SyncToBlockAsync(BigInteger targetBlock, CancellationToken cancellationToken = default);

        Task<BatchDownloadResult> DownloadBatchAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default);

        event EventHandler<BatchSyncProgressEventArgs>? Progress;
        event EventHandler<BatchImportedEventArgs>? BatchImported;
        event EventHandler<SyncErrorEventArgs>? Error;
    }

    public enum BatchSyncState
    {
        Idle,
        FetchingAnchors,
        DownloadingBatch,
        VerifyingBatch,
        ImportingBatch,
        Synced,
        Error
    }

    public class BatchSyncResult
    {
        public bool Success { get; set; }
        public BigInteger StartBlock { get; set; }
        public BigInteger EndBlock { get; set; }
        public int BatchesSynced { get; set; }
        public int BlocksSynced { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BatchDownloadResult
    {
        public bool Success { get; set; }
        public BatchInfo? BatchInfo { get; set; }
        public string? LocalPath { get; set; }
        public string? SourceUrl { get; set; }
        public long BytesDownloaded { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BatchSyncProgressEventArgs : EventArgs
    {
        public BatchSyncState State { get; set; }
        public BigInteger CurrentBlock { get; set; }
        public BigInteger TargetBlock { get; set; }
        public double ProgressPercent { get; set; }
        public string? CurrentBatchId { get; set; }
    }

    public class BatchImportedEventArgs : EventArgs
    {
        public BatchInfo BatchInfo { get; set; } = null!;
        public int BlocksImported { get; set; }
        public TimeSpan ImportDuration { get; set; }
    }

    public class SyncErrorEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public bool Recoverable { get; set; }
    }
}
