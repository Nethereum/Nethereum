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

        /// <summary>
        /// EIP-4844 excess blob gas from the block header. Drives the blob
        /// base fee via <see cref="EVM.Gas.Intrinsic.IBlobGasRule.CalculateBlobBaseFee"/>:
        /// <c>fake_exponential(MIN_BASE_FEE_PER_BLOB_GAS, excessBlobGas, BLOB_BASE_FEE_UPDATE_FRACTION)</c>.
        /// Zero on pre-Cancun blocks. Followers source this from
        /// <see cref="Model.BlockHeader.ExcessBlobGas"/>; the sequencer
        /// derives it from the parent block per EIP-4844.
        /// </summary>
        public long ExcessBlobGas { get; set; }

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
