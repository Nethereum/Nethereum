using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth
{

    ///<Summary>
    /// eth_getBalance
    /// 
    /// Returns the balance of the account of given address.
    /// 
    /// Parameters
    /// 
    /// DATA, 20 Bytes - address to check for balance.
    /// QUANTITY|TAG - integer block number, or the string "latest", "earliest" or "pending", see the default block parameter
    /// params: [
    ///    '0x407d73d8a49eeb85d32cf465507dd71d507100c1',
    ///    'latest'
    /// ]
    /// Returns
    /// 
    /// QUANTITY - integer of the current balance in wei.
    /// 
    /// Example
    /// 
    ///  Request
    /// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getBalance","params":["0x407d73d8a49eeb85d32cf465507dd71d507100c1", "latest"],"id":1}'
    /// 
    ///  Result
    /// {
    ///   "id":1,
    ///   "jsonrpc": "2.0",
    ///   "result": "0x0234c8a3397aab58" // 158972490234375000
    /// }    

    ///</Summary>

   
    public class EthGetBalance : RpcRequestResponseHandler<HexBigInteger>, IDefaultBlock
        {

            public BlockParameter DefaultBlock { get; set; }

            public EthGetBalance(RpcClient client) : base(client, ApiMethods.eth_getBalance.ToString())
            {
                DefaultBlock = BlockParameter.CreateLatest();
            }

            public async Task<HexBigInteger> SendRequestAsync( string address, BlockParameter block, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync( id, address, block);
            }

            public async Task<HexBigInteger> SendRequestAsync( string address, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await SendRequestAsync( address, DefaultBlock, id);
            }
            public RpcRequest BuildRequest(string address, BlockParameter block, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, address, block);
            }
        }
}
