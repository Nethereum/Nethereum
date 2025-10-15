using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class SwapExactOut : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_OUT;

        [Parameter("address", "currencyOut", 1)]
        public string CurrencyOut { get; set; }

        [Parameter("tuple[]", "path", 2)]
        public List<PathKey> Path { get; set; }

        [Parameter("uint128", "amountOut", 3)]
        public BigInteger AmountOut { get; set; }

        [Parameter("uint128", "amountInMaximum", 4)]
        public BigInteger AmountInMaximum { get; set; }
    }

}

