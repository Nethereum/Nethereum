using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.AppChain.Sequencer
{
    public class SequencerConfig
    {
        public string SequencerAddress { get; set; } = "";
        public string? SequencerPrivateKey { get; set; }
        public int BlockTimeMs { get; set; } = 1000;
        public int MaxTransactionsPerBlock { get; set; } = 1000;
        public int MaxMessagesPerBlock { get; set; } = 50;
        public int MaxPoolSize { get; set; } = 50_000;
        public int MaxTxsPerSender { get; set; } = 1_000;
        public bool AllowEmptyBlocks { get; set; } = false;
        public BlockProductionMode BlockProductionMode { get; set; } = BlockProductionMode.Interval;
        public PolicyConfig Policy { get; set; } = new PolicyConfig();
        public BatchProductionConfig BatchProduction { get; set; } = new BatchProductionConfig();

        public static SequencerConfig Default => new SequencerConfig
        {
            BlockTimeMs = 1000,
            MaxTransactionsPerBlock = 1000,
            AllowEmptyBlocks = false,
            BlockProductionMode = BlockProductionMode.Interval,
            Policy = PolicyConfig.Default,
            BatchProduction = BatchProductionConfig.Default
        };

        public static SequencerConfig OnDemand => new SequencerConfig
        {
            BlockTimeMs = 0,
            MaxTransactionsPerBlock = 1000,
            BlockProductionMode = BlockProductionMode.OnDemand,
            Policy = PolicyConfig.Default,
            BatchProduction = BatchProductionConfig.Default
        };
    }

    public class BatchProductionConfig
    {
        public bool Enabled { get; set; } = false;
        public int BatchCadence { get; set; } = 100;
        public string BatchOutputDirectory { get; set; } = "./batches";
        public bool CompressBatches { get; set; } = true;
        public bool TriggerAnchorOnBatch { get; set; } = true;
        public int TimeThresholdSeconds { get; set; } = 0;

        public static BatchProductionConfig Default => new BatchProductionConfig
        {
            Enabled = false,
            BatchCadence = 100,
            CompressBatches = true,
            TriggerAnchorOnBatch = true
        };

        public static BatchProductionConfig WithCadence(int cadence) => new BatchProductionConfig
        {
            Enabled = true,
            BatchCadence = cadence,
            CompressBatches = true,
            TriggerAnchorOnBatch = true
        };

        public static BatchProductionConfig WithTimeThreshold(int cadence, int timeThresholdSeconds) => new BatchProductionConfig
        {
            Enabled = true,
            BatchCadence = cadence,
            TimeThresholdSeconds = timeThresholdSeconds,
            CompressBatches = true,
            TriggerAnchorOnBatch = true
        };
    }

    public enum BlockProductionMode
    {
        Interval,
        OnDemand
    }

    public class PolicyConfig
    {
        public bool Enabled { get; set; } = false;
        public BigInteger MaxCalldataBytes { get; set; } = 128_000;
        public BigInteger MaxLogBytes { get; set; } = 1_000_000;
        public List<string>? AllowedWriters { get; set; }
        public byte[]? WritersRoot { get; set; }

        public static PolicyConfig Default => new PolicyConfig
        {
            Enabled = false,
            MaxCalldataBytes = 128_000,
            MaxLogBytes = 1_000_000
        };

        public static PolicyConfig OpenAccess => new PolicyConfig
        {
            Enabled = false,
            AllowedWriters = null,
            WritersRoot = null
        };

        public static PolicyConfig RestrictedAccess(List<string> allowedWriters) => new PolicyConfig
        {
            Enabled = true,
            AllowedWriters = allowedWriters
        };
    }
}
