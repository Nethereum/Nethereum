using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Rpc;
using System.Collections;

namespace Nethereum.Unity.Contracts
{
    public interface IContractQueryUnityRequest<TFunctionMessage, TResponse> : IUnityRequest<TResponse>
         where TFunctionMessage : FunctionMessage, new()
         where TResponse : IFunctionOutputDTO, new()
    {
        string DefaultAccount { get; set; }
        IEnumerator Query(string contractAddress, BlockParameter blockParameter = null);
        IEnumerator Query(TFunctionMessage functionMessage, string contractAddress, BlockParameter blockParameter = null);
    }
}