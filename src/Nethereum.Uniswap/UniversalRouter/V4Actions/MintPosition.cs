using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class MintPosition:  V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.MINT_POSITION;

        [Parameter("tuple", "poolKey", 1)]
        public PoolKey PoolKey { get; set; }

        [Parameter("int24", "tickLower", 2)]
        public int TickLower { get; set; }

        [Parameter("int24", "tickUpper", 3)]
        public int TickUpper { get; set; }

        [Parameter("uint256", "liquidity", 4)]
        public BigInteger Liquidity { get; set; }

        [Parameter("uint128", "amount0Max", 5)]
        public BigInteger Amount0Max { get; set; }

        [Parameter("uint128", "amount1Max", 6)]
        public BigInteger Amount1Max { get; set; }

        [Parameter("address", "recipient", 7)]
        public string Recipient { get; set; }

        [Parameter("bytes", "hookData", 8)]
        public byte[] HookData { get; set; }
    }

}

