using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    public class EthSimulateV1 : RpcRequestResponseHandler<List<EthSimulateBlockResult>>, IDefaultBlock, IEthSimulateV1
    {
        public EthSimulateV1(IClient client) : base(client, ApiMethods.eth_simulateV1.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public Task<List<EthSimulateBlockResult>> SendRequestAsync(EthSimulateInput input, BlockParameter block,
            object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.SendRequestAsync(id, input, block);
        }

        public Task<List<EthSimulateBlockResult>> SendRequestAsync(EthSimulateInput input, object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return base.SendRequestAsync(id, input, DefaultBlock);
        }

        public RpcRequest BuildRequest(EthSimulateInput input, BlockParameter block, object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.BuildRequest(id, input, block);
        }
    }
}
