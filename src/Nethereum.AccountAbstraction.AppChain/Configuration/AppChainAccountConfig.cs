using System.Numerics;

namespace Nethereum.AccountAbstraction.AppChain.Configuration
{
    public class AppChainAccountConfig
    {
        public string Owner { get; set; }
        public BigInteger Salt { get; set; } = 0;
    }
}
