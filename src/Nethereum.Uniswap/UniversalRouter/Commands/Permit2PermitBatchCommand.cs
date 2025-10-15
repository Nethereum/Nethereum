using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class Permit2PermitBatchCommand : UniversalRouterCommandRevertable
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.PERMIT2_PERMIT_BATCH;
        [Parameter("tuple", "permits", 1)]
        public PermitBatch Permits { get; set; }
        [Parameter("bytes", "signature", 2)]
        public byte[] Signature { get; set; }
        
    }
    
}
