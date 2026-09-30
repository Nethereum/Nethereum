namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeMaintenanceConfig
    {
        public bool Verbose { get; set; }

        public bool WipeState { get; set; }

        public bool CompactAll { get; set; }

        public bool RebuildStateFromFlat { get; set; }

        public bool VerifyFlat { get; set; }

        public long VerifyFlatSampleAccountsPerShard { get; set; }
    }
}
