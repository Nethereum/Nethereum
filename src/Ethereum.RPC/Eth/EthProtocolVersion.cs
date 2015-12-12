
using System;

namespace Ethereum.RPC.Eth
{

    ///<Summary>
    /// eth_protocolVersion
/// 
/// Returns the current ethereum protocol version.
/// 
/// Parameters
/// 
/// none
/// 
/// Returns
/// 
/// String - The current ethereum protocol version
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_protocolVersion","params":[],"id":67}'
/// 
///  Result
/// {
///   "id":67,
///   "jsonrpc": "2.0",
///   "result": "54"
/// }    
    ///</Summary>
    public class EthProtocolVersion : GenericRpcRequestResponseHandlerNoParam<String>
    {
            public EthProtocolVersion() : base(ApiMethods.eth_protocolVersion.ToString()) { }
    }

}
            
        