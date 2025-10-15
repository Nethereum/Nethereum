using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class Settle : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.SETTLE;

        [Parameter("address", "currency", 1)]
        public string Currency { get; set; }

        [Parameter("uint256", "amount", 2)]
        public BigInteger Amount { get; set; }

        [Parameter("bool", "payerIsUser", 3)]
        public bool PayerIsUser { get; set; }
    }

}

