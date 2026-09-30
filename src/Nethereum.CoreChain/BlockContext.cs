using System.Numerics;
using Nethereum.EVM.Execution;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class BlockContext : IBlockEnvironment
    {
        public BigInteger BlockNumber { get; set; }
        public long Timestamp { get; set; }
        public string Coinbase { get; set; }
        public BigInteger GasLimit { get; set; }
        public BigInteger BaseFee { get; set; }
        public BigInteger Difficulty { get; set; } = 1;
        public byte[] PrevRandao { get; set; }
        public BigInteger ChainId { get; set; }

        public long ExcessBlobGas { get; set; }

        public ulong? SlotNumber { get; set; }

        EvmUInt256 IBlockEnvironment.BlockNumber => BlockNumber;

        EvmUInt256 IBlockEnvironment.Timestamp => EvmUInt256.FromHeaderScalar(Timestamp);

        EvmUInt256 IBlockEnvironment.BaseFee => BaseFee;

        EvmUInt256 IBlockEnvironment.Difficulty => Difficulty;

        EvmUInt256 IBlockEnvironment.BlockGasLimit => GasLimit;

        EvmUInt256 IBlockEnvironment.ChainId => ChainId;

        EvmUInt256 IBlockEnvironment.ExcessBlobGas => (ulong)ExcessBlobGas;

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
