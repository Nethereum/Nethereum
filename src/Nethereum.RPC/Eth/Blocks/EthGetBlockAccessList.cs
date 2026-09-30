using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public class EthGetBlockAccessList : RpcRequestResponseHandler<List<AccountAccess>>, IDefaultBlock, IEthGetBlockAccessList
    {
        public EthGetBlockAccessList(IClient client) : base(client, ApiMethods.eth_getBlockAccessList.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public Task<List<AccountAccess>> SendRequestAsync(BlockParameter block, object id = null)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.SendRequestAsync(id, block);
        }

        public Task<List<AccountAccess>> SendRequestAsync(string blockHash, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            return base.SendRequestAsync(id, blockHash.EnsureHexPrefix());
        }

        public Task<List<AccountAccess>> SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id, DefaultBlock);
        }
    }
}
