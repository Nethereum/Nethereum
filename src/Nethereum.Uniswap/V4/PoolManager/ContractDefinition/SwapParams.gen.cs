using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.PoolManager.ContractDefinition
{
    public partial class SwapParams : SwapParamsBase { }

    public class SwapParamsBase 
    {
        [Parameter("bool", "zeroForOne", 1)]
        public virtual bool ZeroForOne { get; set; }
        [Parameter("int256", "amountSpecified", 2)]
        public virtual BigInteger AmountSpecified { get; set; }
        [Parameter("uint160", "sqrtPriceLimitX96", 3)]
        public virtual BigInteger SqrtPriceLimitX96 { get; set; }
    }
}
