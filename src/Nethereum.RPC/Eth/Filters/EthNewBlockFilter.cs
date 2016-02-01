
using edjCase.JsonRpc.Client;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Infrastructure;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth.Filters
{

    ///<Summary>
    /// eth_newBlockFilter
/// 
/// Creates a filter in the node, to notify when a new block arrives. To check if the state has changed, call eth_getFilterChanges.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// QUANTITY - A filter id.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_newBlockFilter","params":[],"id":73}'
/// 
///  Result
/// {
///   "id":1,
///   "jsonrpc":  "2.0",
///   "result": "0x1" // 1
/// }    
    ///</Summary>
    public class EthNewBlockFilter : GenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {
            public EthNewBlockFilter(RpcClient client) : base(client, ApiMethods.eth_newBlockFilter.ToString()) { }
    }

}
            
        