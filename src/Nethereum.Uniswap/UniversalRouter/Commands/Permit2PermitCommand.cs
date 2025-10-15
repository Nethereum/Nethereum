using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;

namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class Permit2PermitCommand : UniversalRouterCommandRevertable
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.PERMIT2_PERMIT;

        [Parameter("tuple", "permit", 1)]
        public PermitSingle Permit { get; set; }

        [Parameter("bytes", "signature", 2)]
        public byte[] Signature { get; set; }

       


    }
    
}
