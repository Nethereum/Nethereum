using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    /// <Summary>
    ///     eth_getStorageAt
    ///     Returns the value from a storage position at a given address.
    ///     Parameters
    ///     DATA, 20 Bytes - address of the storage.
    ///     QUANTITY - integer of the position in the storage.
    ///     QUANTITY|TAG - integer block number, or the string "latest", "earliest" or "pending", see the default block
    ///     parameter
    ///     params: [
    ///     '0x407d73d8a49eeb85d32cf465507dd71d507100c1',
    ///     '0x0', // storage position at 0
    ///     '0x2' // state at block number 2
    ///     ]
    ///     Returns
    ///     DATA - the value at this storage position.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getStorageAt","params":["0x407d73d8a49eeb85d32cf465507dd71d507100c1", "0x0",
    ///     "0x2"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x03"
    ///     }
    /// </Summary>
    public class EthGetStorageAt : RpcRequestResponseHandler<string>, IDefaultBlock
    {
        public EthGetStorageAt(IClient client) : base(client, ApiMethods.eth_getStorageAt.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public async Task<string> SendRequestAsync(string address, HexBigInteger position, BlockParameter block,
            object id = null)
        {
            return await base.SendRequestAsync(id, address, position, block);
        }

        public async Task<string> SendRequestAsync(string address, HexBigInteger position, object id = null)
        {
            return await base.SendRequestAsync(id, address, position, DefaultBlock);
        }

        public RpcRequest BuildRequest(string address, HexBigInteger position, BlockParameter block, object id = null)
        {
            return base.BuildRequest(id, address, position, block);
        }
    }
}