using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class UnwrapWethCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.UNWRAP_WETH;

        [Parameter("address", "recipient", 1)]
        public string Recipient { get; set; }

        [Parameter("uint256", "amountMinimum", 2)]
        public BigInteger AmountMinimum { get; set; }
    }
    
}
