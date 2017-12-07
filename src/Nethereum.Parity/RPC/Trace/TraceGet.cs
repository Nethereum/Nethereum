

using System;
using Nethereum.Hex.HexTypes;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{

    ///<Summary>
       /// Returns trace at given position.    
    ///</Summary>
    public class TraceGet : RpcRequestResponseHandler<JObject>
        {
            public TraceGet(IClient client) : base(client, ApiMethods.trace_get.ToString()) { }

            public async Task<JObject> SendRequestAsync(string transactionHash, HexBigInteger[] index, object id = null)
            {
                return await base.SendRequestAsync(id, transactionHash, index);
            }
            public RpcRequest BuildRequest(string transactionHash, HexBigInteger[] index, object id = null)
            {
                return base.BuildRequest(id, transactionHash, index);
            }
        }

    }

