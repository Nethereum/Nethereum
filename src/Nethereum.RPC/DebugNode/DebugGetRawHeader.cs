using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.DebugNode
{
    public class DebugGetRawHeader : RpcRequestResponseHandler<string>, IDebugGetRawHeader
    {
        public DebugGetRawHeader(IClient client)
            : base(client, ApiMethods.debug_getRawTransaction.ToString())
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