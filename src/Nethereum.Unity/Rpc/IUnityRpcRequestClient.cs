using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System.Collections;

namespace Nethereum.Unity.Rpc
{
    public interface IUnityRpcRequestClient : IUnityRequest<RpcResponseMessage>
    {
        JsonSerializerSettings JsonSerializerSettings { get; }
        IEnumerator SendRequest(RpcRequest request);
    }
}