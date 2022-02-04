using Nethereum.Quorum.RPC.ContractExtensions;

namespace Nethereum.Quorum.RPC.Services
{
    public interface IContractExtensionsService
    {
        IQuorumExtensionActiveExtensionContracts ActiveExtensionContracts { get; }
        IQuorumExtensionApproveExtension ApproveExtension { get; }
        IQuorumExtensionCancelExtension CancelExtension { get; }
        IQuorumExtensionExtendContract ExtendContract { get; }
        IQuorumExtensionGetExtensionStatus GetExtensionStatus { get; }
    }
}