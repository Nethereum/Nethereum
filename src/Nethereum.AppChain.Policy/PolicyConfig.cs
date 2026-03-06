using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.AppChain.Policy
{
    public class PolicyConfig
    {
        public bool Enabled { get; set; } = true;
        public string? PolicyContractAddress { get; set; }
        public string? TargetRpcUrl { get; set; }
        public BigInteger TargetChainId { get; set; }
        public int SyncIntervalMs { get; set; } = 60000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 5000;

        public BigInteger MaxCalldataBytes { get; set; } = 128_000;
        public BigInteger MaxLogBytes { get; set; } = 1_000_000;
        public BigInteger BlockGasLimit { get; set; } = 30_000_000;

        public List<string>? AllowedWriters { get; set; }
        public byte[]? WritersRoot { get; set; }
        public byte[]? AdminsRoot { get; set; }
        public byte[]? BlacklistRoot { get; set; }
        public BigInteger Epoch { get; set; }
    }
}
