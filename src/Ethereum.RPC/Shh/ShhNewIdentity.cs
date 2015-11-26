
using System;

namespace Ethereum.RPC
{

    ///<Summary>
    /// shh_newIdentity
/// 
/// Creates new whisper identity in the client.
/// 
/// Parameters
/// 
/// none
/// 
/// Returns
/// 
/// DATA, 60 Bytes - the address of the new identiy.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"shh_newIdentity","params":[],"id":73}'
/// 
///  Result
/// {
///   "id":1,
///   "jsonrpc": "2.0",
///   "result": "0xc931d93e97ab07fe42d923478ba2465f283f440fd6cabea4dd7a2c807108f651b7135d1d6ca9007d5b68aa497e4619ac10aa3b27726e1863c1fd9b570d99bbaf"
/// }    
    ///</Summary>
    public class ShhNewIdentity : GenericRpcRequestResponseHandlerNoParam<String>
    {
            public ShhNewIdentity() : base(ApiMethods.shh_newIdentity.ToString()) { }
    }

}
            
        