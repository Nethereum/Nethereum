using System;
using System.Threading.Tasks;

using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    public class EthGetProof : RpcRequestResponseHandler<AccountProof>, IDefaultBlock, IEthGetProof
    {
        public EthGetProof(IClient client) : base(client, ApiMethods.eth_getProof.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, BlockParameter block,
            object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (storageKeys == null) throw new ArgumentNullException(nameof(storageKeys));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.SendRequestAsync(id, address.EnsureHexPrefix(), storageKeys, block);
        }

        public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (storageKeys == null) throw new ArgumentNullException(nameof(storageKeys));
            return base.SendRequestAsync(id, address.EnsureHexPrefix(), storageKeys, DefaultBlock);
        }

        public RpcRequest BuildRequest(string address, string[] storageKeys, BlockParameter block, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (storageKeys == null) throw new ArgumentNullException(nameof(storageKeys));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.BuildRequest(id, address.EnsureHexPrefix(), storageKeys, block);
        }
    }
}