using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Sync
{
    public interface ILiveBlockSync
    {
        BigInteger LocalTip { get; }
        BigInteger RemoteTip { get; }
        LiveSyncState State { get; }

        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();

        Task<LiveSyncResult> SyncToLatestAsync(CancellationToken cancellationToken = default);
        Task<LiveSyncResult> SyncToBlockAsync(BigInteger targetBlock, CancellationToken cancellationToken = default);

        Task<BigInteger> GetRemoteTipAsync(CancellationToken cancellationToken = default);
        Task<LiveBlockData?> FetchBlockAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);

        event EventHandler<LiveBlockImportedEventArgs>? BlockImported;
        event EventHandler<LiveSyncErrorEventArgs>? Error;
    }

    public enum LiveSyncState
    {
        Idle,
        Syncing,
        FollowingHead,
        Error
    }

    public class LiveBlockData
    {
        public BlockHeader Header { get; set; } = null!;
        public List<ISignedTransaction> Transactions { get; set; } = new();
        public List<Receipt> Receipts { get; set; } = new();
        public byte[] BlockHash { get; set; } = Array.Empty<byte>();
        public bool IsSoft { get; set; } = true;
    }

    public class LiveSyncResult
    {
        public bool Success { get; set; }
        public BigInteger StartBlock { get; set; }
        public BigInteger EndBlock { get; set; }
        public int BlocksSynced { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class LiveBlockImportedEventArgs : EventArgs
    {
        public BigInteger BlockNumber { get; set; }
        public byte[] BlockHash { get; set; } = Array.Empty<byte>();
        public bool IsSoft { get; set; }
        public int TransactionCount { get; set; }
    }

    public class LiveSyncErrorEventArgs : EventArgs
    {
        public string Message { get; set; } = "";
        public Exception? Exception { get; set; }
        public bool Recoverable { get; set; }
    }
}
