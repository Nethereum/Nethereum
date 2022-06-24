using System.Numerics;

namespace Nethereum.RPC.Chain
{
    public class ChainFeature
    {
        public BigInteger ChainId { get; set; }
        public bool SupportEIP1559 { get; set; } = true;
        public bool SupportEIP155 { get; set; } = true;
        public string ChainName { get; set; }
    }

}
