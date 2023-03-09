using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Metamask
{
    public class MetamaskWebglCoroutineRequestRpcClientFactory : IUnityRpcRequestClientFactory
    {
        public MetamaskWebglCoroutineRequestRpcClientFactory(string account = null, JsonSerializerSettings jsonSerializerSettings = null, int timeOuMiliseconds = WaitUntilRequestResponse.DefaultTimeOutMilliSeconds)
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
            return new MetamaskWebglCoroutineRequestRpcClient(Account, JsonSerializerSettings, TimeOutMilliseconds);
        }
    }
}



