using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.JsonRpc.UnityClient
{
    public interface IUnityRpcRequestClientFactory
    {
        IUnityRpcRequestClient CreateUnityRpcClient();
    }
}