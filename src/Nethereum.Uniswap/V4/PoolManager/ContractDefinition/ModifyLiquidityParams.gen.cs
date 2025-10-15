using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.PoolManager.ContractDefinition
{
    public partial class ModifyLiquidityParams : ModifyLiquidityParamsBase { }

    public class ModifyLiquidityParamsBase 
    {
        [Parameter("int24", "tickLower", 1)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 2)]
        public virtual int TickUpper { get; set; }
        [Parameter("int256", "liquidityDelta", 3)]
        public virtual BigInteger LiquidityDelta { get; set; }
        [Parameter("bytes32", "salt", 4)]
        public virtual byte[] Salt { get; set; }
    }
}
