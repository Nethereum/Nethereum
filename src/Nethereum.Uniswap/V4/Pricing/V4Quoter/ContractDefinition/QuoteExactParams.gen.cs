using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition
{
    public partial class QuoteExactParams : QuoteExactParamsBase { }

    public class QuoteExactParamsBase 
    {
        [Parameter("address", "exactCurrency", 1)]
        public virtual string ExactCurrency { get; set; }
        [Parameter("tuple[]", "path", 2)]
        public virtual List<PathKey> Path { get; set; }
        [Parameter("uint128", "exactAmount", 3)]
        public virtual BigInteger ExactAmount { get; set; }
    }
}
