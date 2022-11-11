using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.DebugNode
{
    public class DebugGetRawBlock : RpcRequestResponseHandler<string>, IDebugGetRawBlock
    {
        public DebugGetRawBlock(IClient client)
            : base(client, ApiMethods.debug_getRawHeader.ToString())
        {
        }

        public Task<string> SendRequestAsync(BlockParameter block, object id = null)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.SendRequestAsync(id, block);
        }

        public Task<string> SendRequestAsync(object id = null)
        {
            return SendRequestAsync(BlockParameter.CreateLatest(), id);
        }

        public RpcRequest BuildRequest(BlockParameter block, object id = null)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.BuildRequest(id, block);
        }
    }
}