using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class TransferCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.TRANSFER;

        [Parameter("address", "token", 1)]
        public string Token { get; set; }

        [Parameter("address", "recipient", 2)]
        public string Recipient { get; set; }

        [Parameter("uint256", "amount", 3)]
        public BigInteger Amount { get; set; }

       
    }
    
}
