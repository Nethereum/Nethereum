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

        public LightClientConfigSection? LightClient { get; set; }
    }

    public class LightClientConfigSection
    {
        public string? BeaconEndpoint { get; set; }
        public string? WeakSubjectivityRoot { get; set; }
        public string? GenesisValidatorsRoot { get; set; }
    }
}
