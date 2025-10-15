using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class BurnPosition : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.BURN_POSITION;

        [Parameter("uint256", "tokenId", 1)]
        public BigInteger TokenId { get; set; }

        [Parameter("uint128", "amount0Min", 2)]
        public BigInteger Amount0Min { get; set; }

        [Parameter("uint128", "amount1Min", 3)]
        public BigInteger Amount1Min { get; set; }

        [Parameter("bytes", "hookData", 4)]
        public byte[] HookData { get; set; }
    }

}

