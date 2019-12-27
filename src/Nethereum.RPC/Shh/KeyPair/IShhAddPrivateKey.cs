using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.KeyPair
{
    public interface IShhAddPrivateKey
    {
        Task<string> SendRequestAsync(string privateKey, object id = null);
        RpcRequest BuildRequest(string privateKey, object id = null);
    }
}