using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class TakePair : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.TAKE_PAIR;

        [Parameter("address", "currency0", 1)]
        public string Currency0 { get; set; }

        [Parameter("address", "currency1", 2)]
        public string Currency1 { get; set; }

        [Parameter("address", "recipient", 3)]
        public string Recipient { get; set; }
    }

}
