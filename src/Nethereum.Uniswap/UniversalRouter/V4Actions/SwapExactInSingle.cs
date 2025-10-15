using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class SwapExactInSingle : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_IN_SINGLE;

        [Parameter("tuple", "poolKey", 1)]
        public PoolKey PoolKey { get; set; }

        [Parameter("bool", "zeroForOne", 2)]
        public bool ZeroForOne { get; set; }

        [Parameter("uint128", "amountIn", 3)]
        public BigInteger AmountIn { get; set; }

        [Parameter("uint128", "amountOutMinimum", 4)]
        public BigInteger AmountOutMinimum { get; set; }

        [Parameter("bytes", "hookData", 5)]
        public byte[] HookData { get; set; }
    }

}

