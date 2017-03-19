
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Main
{

    ///<Summary>
    /// parity_versionInfo
/// 
/// Provides information about running version of Parity.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Object - Information on current version.
/// hash: Hash - 20 Byte hash of the current build.
/// track: String - Track on which it was released, one of: "stable", "beta", "nightly", "testing", "null" (unknown or self-built).
/// version: Object - Version number composed of major, minor and patch integers.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_versionInfo","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": {
///     "hash": "0x2ae8b4ca278dd7b896090366615fef81cbbbc0e0",
///     "track": "null",
///     "version": {
///       "major": 1,
///       "minor": 6,
///       "patch": 0
///     }
///   }
/// }    
    ///</Summary>
    public class ParityVersionInfo : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
            public ParityVersionInfo(IClient client) : base(client, ApiMethods.parity_versionInfo.ToString()) { }
    }

}
            
        