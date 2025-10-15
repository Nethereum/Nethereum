using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V3.QuoterV2.ContractDefinition
{
    public partial class QuoteExactOutputSingleParams : QuoteExactOutputSingleParamsBase { }

    public class QuoteExactOutputSingleParamsBase 
    {
        [Parameter("address", "tokenIn", 1)]
        public virtual string TokenIn { get; set; }
        [Parameter("address", "tokenOut", 2)]
        public virtual string TokenOut { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint24", "fee", 4)]
        public virtual uint Fee { get; set; }
        [Parameter("uint160", "sqrtPriceLimitX96", 5)]
        public virtual BigInteger SqrtPriceLimitX96 { get; set; }
    }
}
