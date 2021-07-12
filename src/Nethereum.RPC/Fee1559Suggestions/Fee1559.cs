using System.Numerics;

namespace Nethereum.RPC.Fee1559Suggestions
{
    public class Fee1559
    {
        public BigInteger? BaseFee { get; set; }
        public BigInteger? MaxPriorityFeePerGas { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }

    }
}