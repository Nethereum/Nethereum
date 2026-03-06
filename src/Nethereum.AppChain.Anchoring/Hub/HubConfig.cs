using System.Numerics;

namespace Nethereum.AppChain.Anchoring.Hub
{
    public class HubConfig
    {
        public bool Enabled { get; set; }
        public string? HubContractAddress { get; set; }
        public ulong ChainId { get; set; }
        public string? HubRpcUrl { get; set; }
        public BigInteger HubChainId { get; set; }
        public string? SequencerPrivateKey { get; set; }
        public int AnchorCadence { get; set; } = 100;
        public int AnchorIntervalMs { get; set; } = 60000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 5000;
    }
}
