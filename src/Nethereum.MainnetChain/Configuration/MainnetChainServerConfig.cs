namespace Nethereum.MainnetChain.Configuration
{
    public partial class MainnetChainServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8545;
        public int MetricsPort { get; set; } = 0;
        public string? DataDir { get; set; }
        public string? TrustedPeer { get; set; }

        public string? NodeKeyFile { get; set; }

        public bool Verbose { get; set; }

        public bool AllowUnverifiedConsensus { get; set; }

        public int ListenPort { get; set; } = -1;

        public bool WipeState { get; set; }

        public bool CompactAll { get; set; }

        public bool RebuildStateFromFlat { get; set; }

        public bool VerifyFlat { get; set; }

        public long VerifyFlatSampleAccountsPerShard { get; set; }

        public bool SnapBootstrap { get; set; } = false;

        public bool EnableTxSubmission { get; set; } = false;

        public LightClientConfigSection? LightClient { get; set; }
    }

    public class LightClientConfigSection
    {
        public string? BeaconEndpoint { get; set; }

        public bool TrustBeaconWithoutBls { get; set; } = false;

        public string? WeakSubjectivityRoot { get; set; }

        public string? GenesisValidatorsRoot { get; set; }
    }
}
