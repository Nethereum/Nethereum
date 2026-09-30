namespace Nethereum.MainnetChain.Configuration
{
    public enum HistoryBackfillMode
    {
        DuringStateSync,
        AfterStateSync,
        Never,
    }

    public partial class MainnetChainServerConfig
    {
        public ulong StartBlock { get; set; } = 1;
        public ulong Blocks { get; set; } = ulong.MaxValue;
        public int TargetPeers { get; set; } = 16;
        public int HeadersBatch { get; set; } = 192;
        public int BodiesBatch { get; set; } = 64;
        public ulong CheckpointEvery { get; set; } = 50_000;
        public int? KeepLatestCheckpoints { get; set; } = 5;

        public int JournalBlocks { get; set; } = 128;

        public bool PathKeyedState { get; set; } = true;

        public int TrieNodeHistoryBlocks { get; set; } = 128;

        public bool TrieNodeHistoryIndex { get; set; } = true;

        public long BlockCacheSize { get; set; } = 1024L * 1024 * 1024;

        public int FlushCadenceBlocks { get; set; } = 1;

        public bool BulkSync { get; set; }

        public bool SplitHistoryStore { get; set; } = false;

        public int HotWindowBlocks { get; set; } = 128;

        public bool EnableLogIndex { get; set; } = false;

        public bool PromotionEnabled { get; set; } = false;

        public bool UseFreezerHistory { get; set; } = false;

        public string? FreezerHistoryDirectory { get; set; }

        public bool BackgroundFreezeIndexing { get; set; } = false;

        public int FreezerBackgroundDegreeOfParallelism { get; set; } = 0;

        public bool DisableDiscv5 { get; set; }
        public int Discv5Port { get; set; }

        public bool DisableDiscv4 { get; set; }

        public int Discv4Port { get; set; }

        public bool ContinueOnMismatch { get; set; }

        public bool SnapPhase1Only { get; set; } = false;

        public bool SnapPhase1First { get; set; } = false;

        public int? SnapAccountConcurrency { get; set; }

        public int? SnapLargeContractConcurrency { get; set; }

        public bool SnapFinalizeVerify { get; set; } = true;

        public bool SnapEnableFlatReconcile { get; set; } = true;

        public bool BackwardSkeletonPhase1 { get; set; } = true;

        public ulong? HeadersFrom { get; set; }

        public ulong HeadersTo { get; set; } = 0;

        public bool ReceiptBackfill { get; set; } = false;

        public HistoryBackfillMode HistoryBackfill { get; set; } = HistoryBackfillMode.DuringStateSync;

        public bool RunHistoryBackfillDuringStateSync => HistoryBackfill == HistoryBackfillMode.DuringStateSync;

        public bool RunHistoryBackfillAfterStateSync => HistoryBackfill == HistoryBackfillMode.AfterStateSync;

        public int RpcMaxLogBlockRange { get; set; } = 10_000;

        public int RpcMaxLogResults { get; set; } = 10_000;

        public long RpcGasCap { get; set; } = 50_000_000;
    }
}
