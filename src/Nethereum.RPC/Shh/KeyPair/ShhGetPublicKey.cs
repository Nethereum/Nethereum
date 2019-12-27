using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.KeyPair
{
    public class ShhGetPublicKey : RpcRequestResponseHandler<string>, IShhGetPublicKey
    {
        public ShhGetPublicKey(IClient client) : base(client, ApiMethods.shh_getPublicKey.ToString())
        {
        }

        public RpcRequest BuildRequest(string keypair, object id = null)
        {
            if (string.IsNullOrEmpty(keypair)) throw new ArgumentNullException(nameof(keypair));
            return base.BuildRequest(id, keypair);
        }

        public Task<string> SendRequestAsync(string keypair, object id = null)
        {
            if (string.IsNullOrEmpty(keypair)) throw new ArgumentNullException(nameof(keypair));
            return base.SendRequestAsync(id, keypair);
        }
    }
}
