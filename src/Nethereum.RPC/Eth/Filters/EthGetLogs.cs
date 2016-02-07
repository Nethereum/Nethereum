using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth.Filters
{

    ///<Summary>
       /// eth_getLogs
/// 
/// Returns an array of all logs matching a given filter object.
/// 
/// Parameters
/// 
/// Object - the filter object, see eth_newFilter parameters.
/// params: [{
///   "topics": ["0x000000000000000000000000a94f5374fce5edbc8e2a8697c15331677e6ebf0b"]
/// }]
/// Returns
/// 
/// See eth_getFilterChanges
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getLogs","params":[{"topics":["0x000000000000000000000000a94f5374fce5edbc8e2a8697c15331677e6ebf0b"]}],"id":74}'
/// Result see eth_getFilterChanges    
    ///</Summary>
    public class EthGetLogs : RpcRequestResponseHandler<NewFilterLog[]>
        {
            public EthGetLogs(RpcClient client) : base(client,ApiMethods.eth_getLogs.ToString()) { }

            public async Task<NewFilterLog[]> SendRequestAsync(NewFilterInput newFilter, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync(id, newFilter);
            }
            public RpcRequest BuildRequest(NewFilterInput newFilter, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, newFilter);
            }
        }

    }

