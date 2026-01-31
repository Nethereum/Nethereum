using System.Numerics;

namespace Nethereum.CoreChain
{
    public class BlockContext
    {
        public BigInteger BlockNumber { get; set; }
        public long Timestamp { get; set; }
        public string Coinbase { get; set; }
        public BigInteger GasLimit { get; set; }
        public BigInteger BaseFee { get; set; }
        public BigInteger Difficulty { get; set; } = 1;
        public byte[] PrevRandao { get; set; }
        public BigInteger ChainId { get; set; }

        public static BlockContext FromConfig(ChainConfig config, BigInteger blockNumber, long timestamp)
        {
            return new BlockContext
            {
                BlockNumber = blockNumber,
                Timestamp = timestamp,
                Coinbase = config.Coinbase,
                GasLimit = config.BlockGasLimit,
                BaseFee = config.BaseFee,
                ChainId = config.ChainId,
                Difficulty = 1,
                PrevRandao = new byte[32]
            };
        }
    }
}
