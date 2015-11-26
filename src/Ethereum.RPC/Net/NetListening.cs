
using System;

namespace Ethereum.RPC
{

    ///<Summary>
    /// net_listening
/// 
/// Returns true if client is actively listening for network connections.
/// 
/// Parameters
/// 
/// none
/// 
/// Returns
/// 
/// Boolean - true when listening, otherwise false.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"net_listening","params":[],"id":67}'
/// 
///  Result
/// {
///   "id":67,
///   "jsonrpc":"2.0",
///   "result":true
/// }    
    ///</Summary>
    public class NetListening : GenericRpcRequestResponseHandlerNoParam<bool>
    {
            public NetListening() : base(ApiMethods.net_listening.ToString()) { }
    }

}
            
        