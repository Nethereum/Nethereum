using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.ContractExtensions;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class ContractExtensionsService: RpcClientWrapper, IContractExtensionsService
    {
        public ContractExtensionsService(IClient client) : base(client)
        {
            ActiveExtensionContracts = new QuorumExtensionActiveExtensionContracts(client);
            ApproveExtension = new QuorumExtensionApproveExtension(client);
            CancelExtension = new QuorumExtensionCancelExtension(client);
            ExtendContract = new QuorumExtensionExtendContract(client);
            GetExtensionStatus = new QuorumExtensionGetExtensionStatus(client);
        }

        public IQuorumExtensionActiveExtensionContracts ActiveExtensionContracts { get; }
        public IQuorumExtensionApproveExtension ApproveExtension { get; }
        public IQuorumExtensionCancelExtension CancelExtension { get; }
        public IQuorumExtensionExtendContract ExtendContract { get; }
        public IQuorumExtensionGetExtensionStatus GetExtensionStatus { get; }
    }
}