using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class CloseCurrency: V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.CLOSE_CURRENCY;

        [Parameter("address", "currency", 1)]
        public string Currency { get; set; }
    }

}

