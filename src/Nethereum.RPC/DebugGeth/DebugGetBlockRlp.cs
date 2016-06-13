

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC
{

    ///<Summary>
       /// Retrieves and returns the RLP encoded block by number.    
    ///</Summary>
    public class DebugGetBlockRlp : RpcRequestResponseHandler<string>
        {
            public DebugGetBlockRlp(IClient client) : base(client,ApiMethods.debug_getBlockRlp.ToString()) { }

            public Task<string> SendRequestAsync(long blockNumber, object id = null)
            {
                return base.SendRequestAsync(id, blockNumber);
            }
            public RpcRequest BuildRequest(long blockNumber, object id = null)
            {
                return base.BuildRequest(id, blockNumber);
            }
        }

    }

