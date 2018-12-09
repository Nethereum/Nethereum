using System;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     eth_getTransactionCount
    ///     Returns the number of transactions sent from an address.
    ///     Parameters
    ///     DATA, 20 Bytes - address.
    ///     QUANTITY|TAG - integer block number, or the string "latest", "earliest" or "pending", see the default block
    ///     parameter
    ///     params: [
    ///     '0x407d73d8a49eeb85d32cf465507dd71d507100c1',
    ///     'latest' // state at the latest block
    ///     ]
    ///     Returns
    ///     QUANTITY - integer of the number of transactions send from this address.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getTransactionCount","params":["0x407d73d8a49eeb85d32cf465507dd71d507100c1","latest"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x1" // 1
    ///     }
    /// </Summary>
    public class EthGetTransactionCount : RpcRequestResponseHandler<HexBigInteger>, IDefaultBlock, IEthGetTransactionCount
    {
        public EthGetTransactionCount(IClient client) : base(client, ApiMethods.eth_getTransactionCount.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public Task<HexBigInteger> SendRequestAsync(string address, BlockParameter block,
            object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.SendRequestAsync(id, address.EnsureHexPrefix(), block);
        }

        public Task<HexBigInteger> SendRequestAsync(string address,
            object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return base.SendRequestAsync(id, address.EnsureHexPrefix(), DefaultBlock);
        }

        public RpcRequest BuildRequest(string address, BlockParameter block, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.BuildRequest(id, address.EnsureHexPrefix(), block);
        }
    }
}