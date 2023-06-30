using Nethereum.Unity.Rpc;

namespace Nethereum.Unity.Contracts
{
    public interface IContractTransactionUnityRequestFactory
    {
        IUnityRpcRequestClientFactory UnityRpcClientFactory { get; }

        IContractTransactionUnityRequest CreateContractTransactionUnityRequest();
    }
}