using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V3.QuoterV2.ContractDefinition
{
    public partial class QuoteExactInputSingleParams : QuoteExactInputSingleParamsBase { }

    public class QuoteExactInputSingleParamsBase 
    {
        [Parameter("address", "tokenIn", 1)]
        public virtual string TokenIn { get; set; }
        [Parameter("address", "tokenOut", 2)]
        public virtual string TokenOut { get; set; }
        [Parameter("uint256", "amountIn", 3)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("uint24", "fee", 4)]
        public virtual uint Fee { get; set; }
        [Parameter("uint160", "sqrtPriceLimitX96", 5)]
        public virtual BigInteger SqrtPriceLimitX96 { get; set; }
    }
}
