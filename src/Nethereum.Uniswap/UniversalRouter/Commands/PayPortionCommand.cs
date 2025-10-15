using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class PayPortionCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.PAY_PORTION;

        [Parameter("address", "token", 1)]
        public string Token { get; set; }

        [Parameter("address", "recipient", 2)]
        public string Recipient { get; set; }

        [Parameter("uint256", "percentageBasisPoints", 3)]
        public BigInteger PercentageBasisPoints { get; set; }

       
    }
    
}
