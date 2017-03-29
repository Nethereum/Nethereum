using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.BlockAuthoring
{

    ///<Summary>
    /// parity_defaultExtraData
/// 
/// Returns the default extra data
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Data - Extra data
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_defaultExtraData","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": "0xd5830106008650617269747986312e31342e30826c69"
/// }    
    ///</Summary>
    public class ParityDefaultExtraData : GenericRpcRequestResponseHandlerNoParam<string>
    {
            public ParityDefaultExtraData(IClient client) : base(client, ApiMethods.parity_defaultExtraData.ToString()) { }
    }

}
            
        