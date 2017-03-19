
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Main
{

    ///<Summary>
    /// parity_releasesInfo
/// 
/// returns a ReleasesInfo object describing the current status of releases
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Object - Information on current releases, null if not available.
/// fork: Quantity - Block number representing the last known fork for this chain, which may be in the future.
/// minor: Object - Information about latest minor update to current version, null if this is the latest minor version.
/// track: Object - Information about the latest release in this track.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_releasesInfo","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": null
/// }    
    ///</Summary>
    public class ParityReleasesInfo : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
            public ParityReleasesInfo(IClient client) : base(client, ApiMethods.parity_releasesInfo.ToString()) { }
    }

}
            
        