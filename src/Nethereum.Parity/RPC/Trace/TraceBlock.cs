

using System;
using Nethereum.Hex.HexTypes;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    ///<Summary>
    /// Returns traces created at given block    
    ///</Summary>
    public class TraceBlock : RpcRequestResponseHandler<JArray>
    {
        public TraceBlock(IClient client) : base(client, ApiMethods.trace_block.ToString())
        {
        }

        public async Task<JArray> SendRequestAsync(HexBigInteger blockNumber, object id = null)
        {
            return await base.SendRequestAsync(id, blockNumber);
        }

        public RpcRequest BuildRequest(HexBigInteger blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber);
        }
    }
}

