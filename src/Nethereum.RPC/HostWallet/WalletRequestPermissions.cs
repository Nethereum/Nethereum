using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.RPC.HostWallet
{
    public class WalletRequestPermissions : RpcRequestResponseHandler<JObject>, IWalletRequestPermissions
    {
        public WalletRequestPermissions() : this(null)
        {
        }

        public WalletRequestPermissions(IClient client) : base(client, ApiMethods.wallet_requestPermissions.ToString())
        {

        }

        public Task<JObject> SendRequestAsync(string[] methods, object id = null)
        {
            var dictionary = ConvertToDictionary(methods);
            return base.SendRequestAsync(id, dictionary);
        }

        private static Dictionary<string, object> ConvertToDictionary(string[] methods)
        {
            var dictionary = new Dictionary<string, object>();
            foreach (var method in methods)
            {
                dictionary.Add(method, null);
            }
            return dictionary;
        }

        public RpcRequest BuildRequest(string[] methods, object id = null)
        {
            var dictionary = ConvertToDictionary(methods);
            return base.BuildRequest(id, dictionary);
        }
    }
}
