using System.Numerics;

namespace Nethereum.GSN.Models
{
    public class RelayOnChain
    {
        public string Address { get; set; }
        public string Url { get; set; }
        public BigInteger Fee { get; set; }
        public BigInteger Stake { get; set; }
        public BigInteger UnstakeDelay { get; set; }
    }
}
