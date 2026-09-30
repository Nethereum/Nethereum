using Nethereum.EVM.Execution;
using Nethereum.Util;

namespace Nethereum.EVM.Witness
{
    public partial class BlockWitnessData : IBlockEnvironment
    {
        EvmUInt256 IBlockEnvironment.BlockNumber => BlockNumber;

        EvmUInt256 IBlockEnvironment.Timestamp => Timestamp;

        EvmUInt256 IBlockEnvironment.BaseFee => BaseFee;

        EvmUInt256 IBlockEnvironment.BlockGasLimit => BlockGasLimit;

        EvmUInt256 IBlockEnvironment.ChainId => ChainId;

        EvmUInt256 IBlockEnvironment.ExcessBlobGas => ExcessBlobGas ?? 0;

        EvmUInt256 IBlockEnvironment.Difficulty => MixHashWhereDifficultyIsZero();

        private EvmUInt256 MixHashWhereDifficultyIsZero()
        {
            var difficulty = Difficulty != null ? EvmUInt256.FromBigEndian(Difficulty) : EvmUInt256.Zero;

            return difficulty.IsZero && MixHash != null && MixHash.Length > 0
                ? EvmUInt256.FromBigEndian(MixHash)
                : difficulty;
        }
    }
}
