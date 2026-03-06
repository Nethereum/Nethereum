using System.Numerics;

namespace Nethereum.AppChain.Anchoring
{
    public class AnchorConfig
    {
        public bool Enabled { get; set; } = true;
        public int AnchorCadence { get; set; } = 100;
        public string? TargetRpcUrl { get; set; }
        public BigInteger TargetChainId { get; set; }
        public string? AnchorContractAddress { get; set; }
        public string? SequencerPrivateKey { get; set; }
        public int AnchorIntervalMs { get; set; } = 60000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 5000;
    }
}
