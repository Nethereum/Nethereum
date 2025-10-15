using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class TakePortion : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.TAKE_PORTION;

        [Parameter("address", "currency", 1)]
        public string Currency { get; set; }

        [Parameter("address", "recipient", 2)]
        public string Recipient { get; set; }

        [Parameter("uint256", "bips", 3)]
        public BigInteger Bips { get; set; }
    }

}

