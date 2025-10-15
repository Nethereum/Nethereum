using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class SwapExactIn : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_IN;

        [Parameter("address", "currencyIn", 1)]
        public string CurrencyIn { get; set; }

        [Parameter("tuple[]", "path", 2)]
        public List<PathKey> Path { get; set; }

        [Parameter("uint128", "amountIn", 3)]
        public BigInteger AmountIn { get; set; }

        [Parameter("uint128", "amountOutMinimum", 4)]
        public BigInteger AmountOutMinimum { get; set; }
    }

}

