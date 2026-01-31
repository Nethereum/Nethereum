using System.Numerics;

namespace Nethereum.CoreChain
{
    public class BlockProductionOptions
    {
        public long Timestamp { get; set; }
        public string Coinbase { get; set; }
        public BigInteger BaseFee { get; set; }
        public BigInteger BlockGasLimit { get; set; }
        public BigInteger Difficulty { get; set; }
        public byte[] PrevRandao { get; set; }
        public byte[] ExtraData { get; set; }
    }
}
