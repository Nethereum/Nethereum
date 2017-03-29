using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Admin
{

    ///<Summary>
    /// parity_pendingTransactionsStats
/// 
/// Returns propagation stats for transactions in the queue.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Object - mapping of transaction hashes to stats.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_pendingTransactionsStats","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": {
///     "0xdff37270050bcfba242116c745885ce2656094b2d3a0f855649b4a0ee9b5d15a": {
///       "firstSeen": 3032066,
///       "propagatedTo": {
///         "0x605e04a43b1156966b3a3b66b980c87b7f18522f7f712035f84576016be909a2798a438b2b17b1a8c58db314d88539a77419ca4be36148c086900fba487c9d39": 1,
///         "0xbab827781c852ecf52e7c8bf89b806756329f8cbf8d3d011e744a0bc5e3a0b0e1095257af854f3a8415ebe71af11b0c537f8ba797b25972f519e75339d6d1864": 1
///       }
///     }
///   }
/// }    
    ///</Summary>
    public class ParityPendingTransactionsStats : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
            public ParityPendingTransactionsStats(IClient client) : base(client, ApiMethods.parity_pendingTransactionsStats.ToString()) { }
    }

}
            
        