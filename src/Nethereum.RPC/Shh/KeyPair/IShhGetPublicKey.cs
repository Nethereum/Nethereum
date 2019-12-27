using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.KeyPair
{
    public interface IShhGetPublicKey
    {
        Task<string> SendRequestAsync(string keypair, object id = null);
        RpcRequest BuildRequest(string keypair, object id = null);
    }
}
