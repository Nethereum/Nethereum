using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Admin
{

    ///<Summary>
    /// parity_localTransactions
/// 
/// Returns an object of current and past local transactions.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Object - Mapping of transaction hashes to status objects status object.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_localTransactions","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": {
///     "0x09e64eb1ae32bb9ac415ce4ddb3dbad860af72d9377bb5f073c9628ab413c532": {
///       "status": "mined",
///       "transaction": {
///         "from": "0x00a329c0648769a73afac7f9381e08fb43dbea72",
///         "to": "0x00a289b43e1e4825dbedf2a78ba60a640634dc40",
///         "value": "0xfffff",
///         "blockHash": null,
///         "blockNumber": null,
///         "creates": null,
///         "gas": "0xe57e0",
///         "gasPrice": "0x2d20cff33",
///         "hash": "0x09e64eb1ae32bb9ac415ce4ddb3dbad860af72d9377bb5f073c9628ab413c532",
///         "input": "0x",
///         "minBlock": null,
///         "networkId": null,
///         "nonce": "0x0",
///         "publicKey": "0x3fa8c08c65a83f6b4ea3e04e1cc70cbe3cd391499e3e05ab7dedf28aff9afc538200ff93e3f2b2cb5029f03c7ebee820d63a4c5a9541c83acebe293f54cacf0e",
///         "raw": "0xf868808502d20cff33830e57e09400a289b43e1e4825dbedf2a78ba60a640634dc40830fffff801ca034c333b0b91cd832a3414d628e3fea29a00055cebf5ba59f7038c188404c0cf3a0524fd9b35be170439b5ffe89694ae0cfc553cb49d1d8b643239e353351531532",
///         "standardV": "0x1",
///         "v": "0x1c",
///         "r": "0x34c333b0b91cd832a3414d628e3fea29a00055cebf5ba59f7038c188404c0cf3",
///         "s": "0x524fd9b35be170439b5ffe89694ae0cfc553cb49d1d8b643239e353351531532",
///         "transactionIndex": null
///       }
///     },
///     "0x...": { ... }
///   }
/// }    
    ///</Summary>
    public class ParityLocalTransactions : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
            public ParityLocalTransactions(IClient client) : base(client, ApiMethods.parity_localTransactions.ToString()) { }
    }

}
            
        