using System.Numerics;

namespace Nethereum.AccountAbstraction.AppChain.Configuration
{
    public class AppChainAccountConfig
    {
        public string Owner { get; set; }
        public BigInteger Salt { get; set; } = 0;
        public string[]? Guardians { get; set; }
        public int? RecoveryThreshold { get; set; }
        public ulong RecoveryDelay { get; set; } = 48 * 60 * 60; // 48 hours
    }
}
