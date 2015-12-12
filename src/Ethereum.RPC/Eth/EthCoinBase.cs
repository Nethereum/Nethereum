
using System;

namespace Ethereum.RPC.Eth
{

    ///<Summary>
    /// eth_coinbase
/// 
/// Returns the client coinbase address.
/// 
/// Parameters
/// 
/// none
/// 
/// Returns
/// 
/// DATA, 20 bytes - the current coinbase address.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_coinbase","params":[],"id":64}'
/// 
///  Result
/// {
///   "id":64,
///   "jsonrpc": "2.0",
///   "result": "0x407d73d8a49eeb85d32cf465507dd71d507100c1"
/// }    
    ///</Summary>
    public class EthCoinBase : GenericRpcRequestResponseHandlerNoParam<String>
    {
            public EthCoinBase() : base(ApiMethods.eth_coinbase.ToString()) { }
    }

}
            
        