using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Unity.Rpc;

namespace Nethereum.Unity.Contracts
{
    public interface IContractQueryUnityRequestFactory
    {
        IUnityRpcRequestClientFactory UnityRpcClientFactory { get; }

        IContractQueryUnityRequest<TFunctionMessage, TResponse> CreateQueryUnityRequest<TFunctionMessage, TResponse>()
            where TFunctionMessage : FunctionMessage, new()
            where TResponse : IFunctionOutputDTO, new();
    }
}