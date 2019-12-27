using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.KeyPair;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.KeyPair
{
    public class ShhHasKeyPair : RpcRequestResponseHandler<bool>, IShhHasKeyPair
    {
        public ShhHasKeyPair(IClient client) : base(client, ApiMethods.shh_hasKeyPair.ToString())
        {
        }

        public RpcRequest BuildRequest(string keypair, object id = null)
        {
            if (string.IsNullOrEmpty(keypair)) throw new ArgumentNullException(nameof(keypair));
            return base.BuildRequest(id, keypair);
        }

        public Task<bool> SendRequestAsync(string keypair, object id = null)
        {
            if (string.IsNullOrEmpty(keypair)) throw new ArgumentNullException(nameof(keypair));
            return base.SendRequestAsync(id, keypair);
        }
    }
}