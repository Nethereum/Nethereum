using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth.Filters
{

    ///<Summary>
    /// Creates a filter object, based on filter options, to notify when the state changes (logs). To check if the state has changed, call eth_getFilterChanges.
/// 
/// A note on specifying topic filters:
/// 
/// Topics are order-dependent. A transaction with a log with topics [A, B] will be matched by the following topic filters:
/// 
/// [] "anything"
/// [A] "A in first position (and anything after)"
/// [null, B] "anything in first position AND B in second position (and anything after)"
/// [A, B] "A in first position AND B in second position (and anything after)"
/// [[A, B], [A, B]] "(A OR B) in first position AND (A OR B) in second position (and anything after)"
/// Parameters
/// 
/// Object - The filter options:
/// fromBlock: QUANTITY|TAG - (optional, default: "latest") Integer block number, or "latest" for the last mined block or "pending", "earliest" for not yet mined transactions.
/// toBlock: QUANTITY|TAG - (optional, default: "latest") Integer block number, or "latest" for the last mined block or "pending", "earliest" for not yet mined transactions.
/// address: DATA|Array, 20 Bytes - (optional) Contract address or a list of addresses from which logs should originate.
/// topics: Array of DATA, - (optional) Array of 32 Bytes DATA topics. Topics are order-dependent. Each topic can also be an array of DATA with "or" options.
/// params: [{
///   "fromBlock": "0x1",
///   "toBlock": "0x2",
///   "address": "0x8888f1f195afa192cfee860698584c030f4c9db1",
///   "topics": ["0x000000000000000000000000a94f5374fce5edbc8e2a8697c15331677e6ebf0b", null, [0x000000000000000000000000a94f5374fce5edbc8e2a8697c15331677e6ebf0b, 0x000000000000000000000000aff3454fce5edbc8cca8697c15331677e6ebccc]]
/// }]
/// Returns
/// 
/// QUANTITY - A filter id.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_newFilter","params":[{"topics":["0x12341234"]}],"id":73}'
/// 
///  Result
/// {
///   "id":1,
///   "jsonrpc": "2.0",
///   "result": "0x1" // 1
/// }    
    ///</Summary>
    public class EthNewFilter : RpcRequestResponseHandler<HexBigInteger>
        {
            public EthNewFilter(RpcClient client) : base(client, ApiMethods.eth_newFilter.ToString()) { }

            public async Task<HexBigInteger> SendRequestAsync( NewFilterInput newFilterInput, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync( id, newFilterInput);
            }
            public RpcRequest BuildRequest(NewFilterInput newFilterInput, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, newFilterInput);
            }
        }
    }

