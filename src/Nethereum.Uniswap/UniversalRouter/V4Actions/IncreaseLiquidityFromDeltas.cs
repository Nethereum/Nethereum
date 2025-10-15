using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class IncreaseLiquidityFromDeltas: V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.INCREASE_LIQUIDITY_FROM_DELTAS;

        [Parameter("uint256", "positionId", 1)]
        public BigInteger PositionId { get; set; }

        [Parameter("uint256", "amount0", 2)]
        public BigInteger Amount0 { get; set; }

        [Parameter("uint128", "amount1", 3)]
        public BigInteger Amount1 { get; set; }

        [Parameter("uint128", "liquidity", 4)]
        public BigInteger Liquidity { get; set; }

        [Parameter("bytes", "hookData", 5)]
        public byte[] HookData { get; set; }
    }

}

