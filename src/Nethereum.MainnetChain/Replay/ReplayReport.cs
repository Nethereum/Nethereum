using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nethereum.MainnetChain.Replay
{
    public sealed class ReplayReport
    {
        public ulong ExecutedHead { get; set; }
        public ulong RequestedBlocks { get; set; }
        public ulong BlocksExecuted { get; set; }
        public ulong FlushCadenceBlocks { get; set; }
        public bool Succeeded { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public ulong? FailedBlock { get; set; }
        public string? FailureDetail { get; set; }
        public double ElapsedSeconds { get; set; }
        public List<ReplayBlockRecord> Blocks { get; set; } = new();
        public ReplaySummary? Summary { get; set; }
        public ReplayCacheReport? Cache { get; set; }
        public List<string> Diagnostics { get; set; } = new();
    }

    public sealed class ReplayBlockRecord
    {
        public ulong Number { get; set; }
        public long GasUsed { get; set; }
        public int TxCount { get; set; }
        public double WallMs { get; set; }
        public int WindowPosition { get; set; }
        public long AllocBytes { get; set; }
        public int Gen0 { get; set; }
        public int Gen1 { get; set; }
        public int Gen2 { get; set; }
        public long RssBytes { get; set; }
        public double ExecMs { get; set; }
        public double RootMs { get; set; }
        public double PersistMs { get; set; }
        public long AccountReads { get; set; }
        public long StorageReads { get; set; }
        public double ExecutionMs { get; set; }
        public double StateReadMs { get; set; }
        public double StateHashMs { get; set; }
        public double CommitMs { get; set; }
    }

    public sealed class ReplayStageStats
    {
        public double Median { get; set; }
        public double P90 { get; set; }
    }

    public sealed class ReplayWindowPositionStats
    {
        public int Position { get; set; }
        public int Blocks { get; set; }
        public double MedianWallMs { get; set; }
        public double MedianAllocBytes { get; set; }
    }

    public sealed class ReplayRssStats
    {
        public long First { get; set; }
        public long Last { get; set; }
        public double SlopeBytesPerBlock { get; set; }
    }

    public sealed class ReplaySummary
    {
        public ReplayStageStats WallMs { get; set; } = new();
        public ReplayStageStats ExecMs { get; set; } = new();
        public ReplayStageStats RootMs { get; set; } = new();
        public ReplayStageStats PersistMs { get; set; } = new();
        public ReplayStageStats ExecutionMs { get; set; } = new();
        public ReplayStageStats StateReadMs { get; set; } = new();
        public ReplayStageStats StateHashMs { get; set; } = new();
        public ReplayStageStats CommitMs { get; set; } = new();
        public double MedianAllocBytesPerBlock { get; set; }
        public List<ReplayWindowPositionStats> WindowPositions { get; set; } = new();
        public ReplayRssStats Rss { get; set; } = new();
        public long TotalGas { get; set; }
        public double TotalWallSeconds { get; set; }
        public double MegagasPerSecond { get; set; }
    }

    public sealed class ReplayCacheSnapshot
    {
        public string Label { get; set; } = string.Empty;
        public ulong AtBlock { get; set; }
        public long IndexHit { get; set; }
        public long IndexMiss { get; set; }
        public long FilterHit { get; set; }
        public long FilterMiss { get; set; }
        public long DataHit { get; set; }
        public long DataMiss { get; set; }
        public long UsageBytes { get; set; }
    }

    public sealed class ReplayCacheReport
    {
        public List<ReplayCacheSnapshot> Snapshots { get; set; } = new();
        public List<ReplayCacheSnapshot> WindowDeltas { get; set; } = new();
        public ReplayCacheSnapshot? Total { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ReplayReport))]
    public sealed partial class ReplayReportJsonContext : JsonSerializerContext
    {
    }
}
