using Nethereum.Quorum.RPC.Privacy;

namespace Nethereum.Quorum.RPC.Services
{
    public interface IPrivacyService
    {
        IEthDistributePrivateTransaction DistributePrivateTransaction { get; }
        IEthFillTransaction FillTransaction { get; }
        IEthGetContractPrivacyMetadata GetContractPrivacyMetadata { get; }
        IEthGetPrivacyPrecompileAddress GetPrivacyPrecompileAddress { get; }
        IEthGetPrivateTransactionByHash GetPrivateTransactionByHash { get; }
        IEthGetPrivateTransactionReceipt GetPrivateTransactionReceipt { get; }
        IEthGetPSI GetPsi { get; }
        IEthGetQuorumPayload GetQuorumPayload { get; }
        IEthSendRawPrivateTransaction SendRawPrivateTransaction { get; }
        IEthSendTransaction SendTransaction { get; }
    }
}