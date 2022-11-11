using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Metamask
{
    public class MetamaskRequestRpcClientFactory : IUnityRpcRequestClientFactory
    {
        public MetamaskRequestRpcClientFactory(string account = null, JsonSerializerSettings jsonSerializerSettings = null, int timeOuMiliseconds = WaitUntilRequestResponse.DefaultTimeOutMiliSeconds)
        {

            Account = account;
            JsonSerializerSettings = jsonSerializerSettings;
            TimeOuMiliseconds = timeOuMiliseconds;
        }
        public string Account { get; set; }
        public JsonSerializerSettings JsonSerializerSettings { get; }
        public int TimeOuMiliseconds { get; }

        public IUnityRpcRequestClient CreateUnityRpcClient()
        {
            return new MetamaskRequestRpcClient(Account, JsonSerializerSettings, TimeOuMiliseconds);
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



