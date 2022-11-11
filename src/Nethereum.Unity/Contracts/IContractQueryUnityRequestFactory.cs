using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Unity.Contracts
{
    public interface IContractQueryUnityRequestFactory
    {
        IContractQueryUnityRequest<TFunctionMessage, TResponse> CreateQueryUnityRequest<TFunctionMessage, TResponse>()
            where TFunctionMessage : FunctionMessage, new()
            where TResponse : IFunctionOutputDTO, new();
    }
}