
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Main
{

    ///<Summary>
    /// parity_devLogsLevels
/// 
/// Returns current logging level settings. Logging level can be set with --logging and be one of: "" (default), "info", "debug", "warn", "error", "trace".
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// String - undefined
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_devLogsLevels","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": "debug"
/// }    
    ///</Summary>
    public class ParityDevLogsLevels : GenericRpcRequestResponseHandlerNoParam<string>
    {
            public ParityDevLogsLevels(IClient client) : base(client, ApiMethods.parity_devLogsLevels.ToString()) { }
    }

}
            
        