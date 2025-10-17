using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition
{
    public partial class QuoteExactSingleParams : QuoteExactSingleParamsBase { }

    public class QuoteExactSingleParamsBase 
    {
        [Parameter("tuple", "poolKey", 1)]
        public virtual PoolKey PoolKey { get; set; }
        [Parameter("bool", "zeroForOne", 2)]
        public virtual bool ZeroForOne { get; set; }
        [Parameter("uint128", "exactAmount", 3)]
        public virtual BigInteger ExactAmount { get; set; }
        [Parameter("bytes", "hookData", 4)]
        public virtual byte[] HookData { get; set; }
    }
}
