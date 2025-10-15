using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class WrapEthCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.WRAP_ETH;

        [Parameter("address", "recipient", 1)]
        public string Recipient { get; set; }

        [Parameter("uint256", "amount", 2)]
        public BigInteger Amount { get; set; }

       


    }
    
}
