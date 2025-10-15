using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class TakeAll : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.TAKE_ALL;

        [Parameter("address", "currency", 1)]
        public string Currency { get; set; }

        [Parameter("uint256", "minAmount", 2)]
        public BigInteger MinAmount { get; set; }
    }

}

