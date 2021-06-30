using System.Numerics;

namespace Nethereum.RPC.Fee1559Calculators
{
    public class Fee1559
    {
        public BigInteger? BaseFee { get; set; }
        public BigInteger? MaxPriorityFeePerGas { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }

    }
}