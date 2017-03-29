using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Accounts
{

    ///<Summary>
    /// parity_hardwareAccountsInfo
/// 
/// Provides metadata for attached hardware wallets
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Object - Maps account address to metadata.
/// manufacturer: String - Manufacturer
/// name: String - Account name
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_hardwareAccountsInfo","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": {
///     "0x0024d0c7ab4c52f723f3aaf0872b9ea4406846a4": {
///       "manufacturer": "Ledger",
///       "name": "Nano S"
///     }
///   }
/// }    
    ///</Summary>
    public class ParityHardwareAccountsInfo : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
            public ParityHardwareAccountsInfo(IClient client) : base(client, ApiMethods.parity_hardwareAccountsInfo.ToString()) { }
    }

}
            
        