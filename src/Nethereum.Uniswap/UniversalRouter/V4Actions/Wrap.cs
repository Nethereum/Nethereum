using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    
    public class Wrap: V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterV4ActionTypes.WRAP;

        [Parameter("uint256", "amount", 1)]
        public BigInteger Amount { get; set; }
    }

}

