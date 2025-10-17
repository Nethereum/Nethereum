using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition
{
    public partial class PathKey : PathKeyBase { }

    public class PathKeyBase 
    {
        [Parameter("address", "intermediateCurrency", 1)]
        public virtual string IntermediateCurrency { get; set; }
        [Parameter("uint24", "fee", 2)]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 3)]
        public virtual int TickSpacing { get; set; }
        [Parameter("address", "hooks", 4)]
        public virtual string Hooks { get; set; }
        [Parameter("bytes", "hookData", 5)]
        public virtual byte[] HookData { get; set; }
    }
}
