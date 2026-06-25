namespace Nethereum.MainnetChain.Server.Configuration
{
    public class MainnetChainServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8545;
        public int MetricsPort { get; set; } = 0;
        public string? DataDir { get; set; }
        public string? TrustedPeer { get; set; }
        public bool Verbose { get; set; }

        public ulong StartBlock { get; set; } = 1;
        public ulong Blocks { get; set; } = ulong.MaxValue;
        public int TargetPeers { get; set; } = 16;
        public int HeadersBatch { get; set; } = 192;
        public int BodiesBatch { get; set; } = 64;
        public ulong CheckpointEvery { get; set; } = 50_000;
        public int? KeepLatestCheckpoints { get; set; } = 5;
        public int JournalBlocks { get; set; } = 1024;
        public int ListenPort { get; set; } = -1;
        public bool DisableDiscv5 { get; set; }
        public int Discv5Port { get; set; }
        public bool ContinueOnMismatch { get; set; }

        /// <summary>
        /// When true and the data dir has no committed state, run a snap/1
        /// bootstrap against the trusted peer before starting the follower.
        /// Default false until the wiring is exercised against a live peer
        /// over multiple runs — opt in explicitly to avoid surprising existing
        /// deployments.
        /// </summary>
        public bool SnapBootstrap { get; set; } = false;

        /// <summary>
        /// When true, snap-bootstrap Phase 1 runs a headers-only backward skeleton
        /// (headers laid backward from the pivot, parent-hash validated) concurrently
        /// with a body+receipt filler over the persisted headers, instead of the
        /// forward header-fetch backfiller. The skeleton owns the header cursor, the
        /// filler the body cursor. Default false keeps the forward path.
        /// </summary>
        public bool BackwardSkeletonPhase1 { get; set; } = false;

        /// <summary>
        /// When true, runs <c>ReceiptBackfillService</c> as a concurrent
        /// background task alongside the follower. The service re-fetches
        /// receipts via DevP2P over the already-synced range, validates each
        /// batch's Patricia receipts-root against the stored header's
        /// <c>ReceiptHash</c>, and overwrites stored entries with
        /// freshly-computed metadata (<c>contractAddress</c>, <c>gasUsed</c>,
        /// <c>effectiveGasPrice</c>). Idempotent — cursor in metadata
        /// (<c>receipt_backfill_cursor</c>) lets it resume across restarts.
        /// Defaults off; enable to scrub historical receipts after any change
        /// to the receipt-persist path.
        /// </summary>
        public bool ReceiptBackfill { get; set; } = false;

        /// <summary>
        /// Consensus light client configuration. When
        /// <see cref="LightClientConfigSection.BeaconEndpoint"/> is set, the
        /// light client is wired AND becomes the canonical tip source for
        /// snap-bootstrap pivot selection: pivot block = light_client.head -
        /// 64. The pivot is therefore selected from a tip attested by the
        /// beacon sync committee (BLS quorum) rather than from peer-pool
        /// <c>Latest</c> sampling.
        ///
        /// <para>When not set, snap-bootstrap falls back to peer-pool max
        /// sampling (legacy behaviour).</para>
        /// </summary>
        public LightClientConfigSection? LightClient { get; set; }
    }

    public class LightClientConfigSection
    {
        /// <summary>Beacon API endpoint the light client connects to (Lighthouse / Prysm / etc.).</summary>
        public string? BeaconEndpoint { get; set; }

        /// <summary>
        /// When true, the light client skips sync-committee BLS verification (NoopBls) and
        /// trusts the beacon endpoint outright. Only safe when the endpoint is the operator's
        /// own trusted node. Default false = verify BLS via the native Herumi bindings.
        /// </summary>
        public bool TrustBeaconWithoutBls { get; set; } = false;

        /// <summary>Optional weak-subjectivity checkpoint root for bootstrap.</summary>
        public string? WeakSubjectivityRoot { get; set; }

        /// <summary>Optional genesis validators root for fork-version domain derivation.</summary>
        public string? GenesisValidatorsRoot { get; set; }
    }
}
