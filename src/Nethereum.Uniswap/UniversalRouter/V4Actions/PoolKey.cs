using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class PoolKey
    {
        [Parameter("address", "currency0", 1)]
        public string Currency0 { get; set; }

        [Parameter("address", "currency1", 2)]
        public string Currency1 { get; set; }

        [Parameter("uint24", "fee", 3)]
        public uint Fee { get; set; }

        [Parameter("int24", "tickSpacing", 4)]
        public int TickSpacing { get; set; }

        [Parameter("address", "hooks", 5)]
        public string Hooks { get; set; }
    }

}

