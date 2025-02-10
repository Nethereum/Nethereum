using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.AccountAbstraction;

namespace Nethereum.RPC
{
    public class AccountAbstractionBundlerService : RpcClientWrapper, IAccountAbstractionBundlerService
    {
        public IEthChainId ChainId { get; private set; }
        public IEthEstimateUserOperationGas EstimateUserOperationGas { get; private set; }
        public IEthGetUserOperationByHash GetUserOperationByHash { get; private set; }
        public IEthGetUserOperationReceipt GetUserOperationReceipt { get; private set; }
        public IEthSendUserOperation SendUserOperation { get; private set; }
        public IEthSupportedEntryPoints SupportedEntryPoints { get; private set; }


        public AccountAbstractionBundlerService(IClient client) : base(client)
        {
            ChainId = new EthChainId(client);
            EstimateUserOperationGas = new EthEstimateUserOperationGas(client);
            GetUserOperationByHash = new EthGetUserOperationByHash(client);
            GetUserOperationReceipt = new EthGetUserOperationReceipt(client);
            SendUserOperation = new EthSendUserOperation(client);
            SupportedEntryPoints = new EthSupportedEntryPoints(client);
        }
    }
}
