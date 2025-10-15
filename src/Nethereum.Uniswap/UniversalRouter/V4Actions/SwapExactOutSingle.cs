using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class SwapExactOutSingle : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_OUT_SINGLE;

        [Parameter("tuple", "poolKey", 1)]
        public PoolKey PoolKey { get; set; }

        [Parameter("bool", "zeroForOne", 2)]
        public bool ZeroForOne { get; set; }

        [Parameter("uint128", "amountOut", 3)]
        public BigInteger AmountOut { get; set; }

        [Parameter("uint128", "amountInMaximum", 4)]
        public BigInteger AmountInMaximum { get; set; }

        [Parameter("bytes", "hookData", 5)]
        public byte[] HookData { get; set; }
    }

}

