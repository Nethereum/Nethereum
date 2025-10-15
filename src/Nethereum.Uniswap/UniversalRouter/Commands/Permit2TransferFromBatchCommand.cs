using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Uniswap.Permit2.ContractDefinition;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class Permit2TransferFromBatchCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.PERMIT2_TRANSFER_FROM_BATCH;

        [Parameter("tuple[]", "transfers", 1)]
        public AllowanceTransferDetails[] Transfers { get; set; }
    }
    
}
