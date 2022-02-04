using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.Privacy;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class PrivacyService : RpcClientWrapper, IPrivacyService
    {
        public PrivacyService(IClient client) : base(client)
        {
            DistributePrivateTransaction = new EthDistributePrivateTransaction(client);
            FillTransaction=new EthFillTransaction(client);
            GetContractPrivacyMetadata = new EthGetContractPrivacyMetadata(client);
            GetPrivacyPrecompileAddress = new EthGetPrivacyPrecompileAddress(client);
            GetPrivateTransactionByHash = new EthGetPrivateTransactionByHash(client);
            GetPrivateTransactionReceipt = new EthGetPrivateTransactionReceipt(client);
            GetPsi = new EthGetPSI(client);
            GetQuorumPayload = new EthGetQuorumPayload(client);
            SendRawPrivateTransaction = new EthSendRawPrivateTransaction(client);
            SendTransaction = new EthSendTransaction(client);
        }

        public IEthDistributePrivateTransaction DistributePrivateTransaction { get; }
        public IEthFillTransaction FillTransaction { get; }
        public IEthGetContractPrivacyMetadata GetContractPrivacyMetadata { get; }
        public IEthGetPrivacyPrecompileAddress GetPrivacyPrecompileAddress { get; }
        public IEthGetPrivateTransactionByHash  GetPrivateTransactionByHash { get; }
        public IEthGetPrivateTransactionReceipt GetPrivateTransactionReceipt { get; }
        public IEthGetPSI GetPsi { get; }
        public IEthGetQuorumPayload GetQuorumPayload { get; }
        public IEthSendRawPrivateTransaction SendRawPrivateTransaction { get; }
        public IEthSendTransaction SendTransaction { get; }

    }
}