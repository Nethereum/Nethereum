using Nethereum.Hex.HexTypes;
using System.Numerics;

namespace Nethereum.Rsk.RPC.RskEth.DTOs
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "rsk-client", "RskBlockExtendedExtensions")]
    public static class RskBlockExtendedExtensions
    {
        public static BigInteger GetMinimumGasPriceAsBigInteger(this IRskBlockExtended rskBlock)
        {
            return string.IsNullOrEmpty(rskBlock.MinimumGasPriceString) ? 0 : new HexBigInteger(rskBlock.MinimumGasPriceString).Value;
        }
    }
}