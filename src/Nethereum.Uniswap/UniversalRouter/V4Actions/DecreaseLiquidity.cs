using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class DecreaseLiquidity: V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.DECREASE_LIQUIDITY;

        [Parameter("uint256", "tokenId", 1)]
        public BigInteger TokenId { get; set; }

        [Parameter("uint256", "liquidity", 2)]
        public BigInteger Liquidity { get; set; }

        [Parameter("uint128", "amount0Min", 3)]
        public BigInteger Amount0Min { get; set; }

        [Parameter("uint128", "amount1Min", 4)]
        public BigInteger Amount1Min { get; set; }

        [Parameter("bytes", "hookData", 5)]
        public byte[] HookData { get; set; }
    }

}

