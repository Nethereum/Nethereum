using System.Numerics;

namespace Nethereum.CoreChain
{
    public class BlockMinerConfig
    {
        public int MaxTransactionsPerBlock { get; set; } = 100;
        public bool AllowEmptyBlocks { get; set; } = true;
        public BigInteger BlockGasLimit { get; set; } = 30_000_000;
        public BigInteger BaseFee { get; set; } = 0;
        public string Coinbase { get; set; } = "0x0000000000000000000000000000000000000000";
        public BigInteger Difficulty { get; set; } = 0;
    }
}
