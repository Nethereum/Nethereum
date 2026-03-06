using System;
using System.Numerics;

namespace Nethereum.AppChain.Sync
{
    public class BatchSyncConfig
    {
        public int BatchSize { get; set; } = 100;

        public BigInteger ChainId { get; set; }

        public string? SequencerUrl { get; set; }

        public string[]? MirrorUrls { get; set; }

        public string? BatchOutputDirectory { get; set; }

        public string? SnapshotOutputDirectory { get; set; }

        public int SnapshotInterval { get; set; } = 10000;

        public BatchVerificationMode DefaultVerificationMode { get; set; } = BatchVerificationMode.Quick;

        public bool AutoCreateBatches { get; set; } = true;

        public bool AutoAnchorBatches { get; set; } = true;

        public int MaxRetries { get; set; } = 3;

        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan SyncPollInterval { get; set; } = TimeSpan.FromSeconds(10);

        public bool CompressBatches { get; set; } = true;

        public bool CompressSnapshots { get; set; } = true;

        public static BatchSyncConfig Default => new BatchSyncConfig
        {
            BatchSize = 100,
            SnapshotInterval = 10000,
            DefaultVerificationMode = BatchVerificationMode.Quick,
            CompressBatches = true,
            CompressSnapshots = true
        };
    }
}
