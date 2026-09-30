namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeSyncConfig
    {
        public SyncMode Mode { get; set; } = SyncMode.ForwardExecute;

        public string? FollowPeerEnode { get; set; }

        public bool TrustedPeersOnlyTip { get; set; }

        public bool EnablePushedBlocks { get; set; }

        public bool FloorTargetPeerCountByDialPool { get; set; }

        public int HeaderBatchSize { get; set; } = 192;

        public int BodyBatchSize { get; set; } = 64;

        public int MaxInFlightPerPeer { get; set; } = 1;

        public ulong MinPeerLatestBlock { get; set; }

        public bool BulkSync { get; set; }

        public ulong StartBlock { get; set; } = 1;

        public ulong Blocks { get; set; } = ulong.MaxValue;

        public ulong? HeadersFrom { get; set; }

        public ulong HeadersTo { get; set; }

        public ulong CheckpointEvery { get; set; } = 50_000;

        public int? KeepLatestCheckpoints { get; set; } = 5;

        public bool ReceiptBackfill { get; set; }

        public bool ContinueOnMismatch { get; set; }

        public ChainNodeSnapConfig Snap { get; set; } = new ChainNodeSnapConfig();

        public bool SnapBootstrap
        {
            get => Mode == SyncMode.Snap;
            set => Mode = value ? SyncMode.Snap : SyncMode.ForwardExecute;
        }
    }
}
