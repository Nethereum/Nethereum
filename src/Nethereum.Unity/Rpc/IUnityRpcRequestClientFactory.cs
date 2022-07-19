using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.Unity.Rpc
{
    public interface IUnityRpcRequestClientFactory
    {
        IUnityRpcRequestClient CreateUnityRpcClient();
    }
}