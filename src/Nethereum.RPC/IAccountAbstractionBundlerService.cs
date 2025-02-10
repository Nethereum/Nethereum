using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.AccountAbstraction;

namespace Nethereum.RPC
{
    public interface IAccountAbstractionBundlerService
    {
        IEthChainId ChainId { get; }
        IEthEstimateUserOperationGas EstimateUserOperationGas { get; }
        IEthGetUserOperationByHash GetUserOperationByHash { get; }
        IEthGetUserOperationReceipt GetUserOperationReceipt { get; }
        IEthSendUserOperation SendUserOperation { get; }
        IEthSupportedEntryPoints SupportedEntryPoints { get; }
    }
}