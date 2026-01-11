using System.Numerics;

namespace Nethereum.CoreChain
{
    public class ChainConfig
    {
        public BigInteger ChainId { get; set; } = 1337;
        public string Coinbase { get; set; } = "0x0000000000000000000000000000000000000000";
        public BigInteger BlockGasLimit { get; set; } = 30_000_000;
        public BigInteger BaseFee { get; set; } = 1_000_000_000; // 1 Gwei
        public BigInteger SuggestedPriorityFee { get; set; } = 1_000_000_000; // 1 Gwei
        public BigInteger InitialBalance { get; set; } = BigInteger.Parse("10000000000000000000000"); // 10000 ETH
    }
}
