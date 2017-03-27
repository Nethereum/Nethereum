
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Main
{

    ///<Summary>
    /// parity_gasCeilTarget
/// 
/// Returns current target for gas ceiling.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Quantity - Gas ceiling target.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_gasCeilTarget","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": "0x5fdfb0" // 6283184
/// }    
    ///</Summary>
    public class ParityGasCeilTarget : GenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {
            public ParityGasCeilTarget(IClient client) : base(client, ApiMethods.parity_gasCeilTarget.ToString()) { }
    }

}
            
        