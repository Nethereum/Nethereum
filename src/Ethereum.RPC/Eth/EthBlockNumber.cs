
namespace Ethereum.RPC.Eth
{

    ///<Summary>
    /// eth_blockNumber
/// 
/// Returns the number of most recent block.
/// 
/// Parameters
/// 
/// none
/// 
/// Returns
/// 
/// QUANTITY - integer of the current block number the client is on.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":83}'
/// 
///  Result
/// {
///   "id":83,
///   "jsonrpc": "2.0",
///   "result": "0x4b7" // 1207
/// }    
    ///</Summary>
    public class EthBlockNumber : GenericRpcRequestResponseHandlerNoParamBigInt
    {
            public EthBlockNumber() : base(ApiMethods.eth_blockNumber.ToString()) { }
    }

}
            
        