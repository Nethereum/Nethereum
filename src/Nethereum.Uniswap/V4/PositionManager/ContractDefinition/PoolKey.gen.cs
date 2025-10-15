using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.PositionManager.ContractDefinition
{
    public partial class PoolKey : PoolKeyBase { }

    public class PoolKeyBase 
    {
        [Parameter("address", "currency0", 1)]
        public virtual string Currency0 { get; set; }
        [Parameter("address", "currency1", 2)]
        public virtual string Currency1 { get; set; }
        [Parameter("uint24", "fee", 3)]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 4)]
        public virtual int TickSpacing { get; set; }
        [Parameter("address", "hooks", 5)]
        public virtual string Hooks { get; set; }
    }
}
