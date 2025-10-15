using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class Sweep : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SWEEP;

        [Parameter("address", "currency", 1)]
        public string Currency { get; set; }

        [Parameter("address", "recipient", 2)]
        public string Recipient { get; set; }
    }

}

