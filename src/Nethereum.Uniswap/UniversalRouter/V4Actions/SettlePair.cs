using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class SettlePair : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SETTLE_PAIR;

        [Parameter("address", "currency0", 1)]
        public string Currency0 { get; set; }

        [Parameter("address", "currency1", 2)]
        public string Currency1 { get; set; }
    }

}
