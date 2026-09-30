using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Freezer
{
    public interface IBlobBaseFeeFractionResolver
    {
        EvmUInt256 FractionForBlock(BlockHeader header);
    }

    public sealed class CancunBlobBaseFeeFractionResolver : IBlobBaseFeeFractionResolver
    {
        public EvmUInt256 FractionForBlock(BlockHeader header) => BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION;
    }
}
