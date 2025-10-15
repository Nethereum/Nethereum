using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class MintPositionFromDeltas : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.MINT_POSITION_FROM_DELTAS;

        [Parameter("tuple", "poolKey", 1)]
        public PoolKey PoolKey { get; set; }

        [Parameter("int24", "tickLower", 2)]
        public int TickLower { get; set; }

        [Parameter("int24", "tickUpper", 3)]
        public int TickUpper { get; set; }

        [Parameter("uint256", "amount0", 4)]
        public BigInteger Amount0 { get; set; }

        [Parameter("uint128", "amount1", 5)]
        public BigInteger Amount1 { get; set; }

        [Parameter("uint128", "liquidity", 6)]
        public BigInteger Liquidity { get; set; }

        [Parameter("address", "recipient", 7)]
        public string Recipient { get; set; }

        [Parameter("bytes", "hookData", 8)]
        public byte[] HookData { get; set; }
    }

}

