using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.EIP712.Permit2;


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
