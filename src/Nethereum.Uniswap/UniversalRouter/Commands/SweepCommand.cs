using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class SweepCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.SWEEP;

        [Parameter("address", "token", 1)]
        public string Token { get; set; }

        [Parameter("address", "recipient", 2)]
        public string Recipient { get; set; }

        [Parameter("uint256", "amountMinimum", 3)]
        public BigInteger AmountMinimum { get; set; }

        
    }
    
}
