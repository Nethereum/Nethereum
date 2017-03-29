using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.BlockAuthoring
{

    ///<Summary>
    /// parity_transactionsLimit
/// 
/// Changes limit for transactions in queue.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Quantity - Current max number of transactions in queue.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_transactionsLimit","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": 1024
/// }    
    ///</Summary>
    public class ParityTransactionsLimit : GenericRpcRequestResponseHandlerNoParam<int>
    {
            public ParityTransactionsLimit(IClient client) : base(client, ApiMethods.parity_transactionsLimit.ToString()) { }
    }

}
            
        