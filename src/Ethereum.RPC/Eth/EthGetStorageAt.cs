

using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Ethereum.RPC.Eth;
using Ethereum.RPC.Generic;
using RPCRequestResponseHandlers;

namespace Ethereum.RPC
{

    ///<Summary>
       /// eth_getStorageAt
/// 
/// Returns the value from a storage position at a given address.
/// 
/// Parameters
/// 
/// DATA, 20 Bytes - address of the storage.
/// QUANTITY - integer of the position in the storage.
/// QUANTITY|TAG - integer block number, or the string "latest", "earliest" or "pending", see the default block parameter
/// params: [
///    '0x407d73d8a49eeb85d32cf465507dd71d507100c1',
///    '0x0', // storage position at 0
///    '0x2' // state at block number 2
/// ]
/// Returns
/// 
/// DATA - the value at this storage position.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getStorageAt","params":["0x407d73d8a49eeb85d32cf465507dd71d507100c1", "0x0", "0x2"],"id":1}'
/// 
///  Result
/// {
///   "id":1,
///   "jsonrpc": "2.0",
///   "result": "0x03"
/// }    
    ///</Summary>
    public class EthGetStorageAt : RpcRequestResponseHandler<string>
        {
            public EthGetStorageAt() : base(ApiMethods.eth_getStorageAt.ToString()) { }

            public async Task<string> SendRequestAsync(RpcClient client, string address, HexBigInteger position, BlockParameter block, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync(client, id, address, position, block);
            }

            public async Task<string> SendRequestAsync(RpcClient client, string address, HexBigInteger position, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await SendRequestAsync(client, address, position, BlockParameter.CreateLatest(), id);
            }

            public RpcRequest BuildRequest(string address, HexBigInteger position, BlockParameter block, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, address, position, block);
            }
        }

    }

