using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Metamask
{
    public class MetamaskRequestRpcClientFactory : IUnityRpcRequestClientFactory
    {
        public MetamaskRequestRpcClientFactory(string account = null, JsonSerializerSettings jsonSerializerSettings = null, int timeOuMiliseconds = WaitUntilRequestResponse.DefaultTimeOutMilliSeconds)
        {

            Account = account;
            JsonSerializerSettings = jsonSerializerSettings;
            TimeOutMilliseconds = timeOuMiliseconds;
        }
        public string Account { get; set; }
        public JsonSerializerSettings JsonSerializerSettings { get; }
        public int TimeOutMilliseconds { get; }

        public IUnityRpcRequestClient CreateUnityRpcClient()
        {
            return new MetamaskRequestRpcClient(Account, JsonSerializerSettings, TimeOutMilliseconds);
        }
    }

    public class MetamaskRpcRequestMessage : RpcRequestMessage
    {
        public MetamaskRpcRequestMessage(object id, string method, string from, params object[] parameterList) : base(id, method,
            parameterList)
        {
            From = from;
        }

        [JsonProperty("from")]
        public string From { get; private set; }
    }
}



