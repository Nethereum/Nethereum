using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    public class EthCreateAccessList : RpcRequestResponseHandler<AccessListGasUsed>, IDefaultBlock, IEthCreateAccessList
    {
        public EthCreateAccessList(IClient client) : base(client, ApiMethods.eth_createAccessList.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public Task<AccessListGasUsed> SendRequestAsync(TransactionInput transactionInput, BlockParameter block,
            object id = null)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.SendRequestAsync(id, transactionInput, block);
        }

        public Task<AccessListGasUsed> SendRequestAsync(TransactionInput transactionInput, object id = null)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return base.SendRequestAsync(id, transactionInput, DefaultBlock);
        }

        public RpcRequest BuildRequest(TransactionInput transactionInput, BlockParameter block, object id = null)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.BuildRequest(id, transactionInput, block);
        }
    }
}