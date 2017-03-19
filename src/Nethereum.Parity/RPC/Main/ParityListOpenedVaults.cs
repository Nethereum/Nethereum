
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Main
{

    ///<Summary>
    /// parity_listOpenedVaults
/// 
/// Returns a list of all opened vaults
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Array - Names of all opened vaults
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_listOpenedVaults","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": "['Personal']"
/// }    
    ///</Summary>
    public class ParityListOpenedVaults : GenericRpcRequestResponseHandlerNoParam<string[]>
    {
            public ParityListOpenedVaults(IClient client) : base(client, ApiMethods.parity_listOpenedVaults.ToString()) { }
    }

}
            
        