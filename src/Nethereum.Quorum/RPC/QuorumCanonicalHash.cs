using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Quorum.RPC
{
    public class QuorumCanonicalHash : RpcRequestResponseHandler<string>
    {
        public QuorumCanonicalHash(IClient client) : base(client, ApiMethods.quorum_canonicalHash.ToString())
        {
        }

        public Task<string> SendRequestAsync(HexBigInteger blockNumber, object id = null)
        {
            if (blockNumber == null) throw new ArgumentNullException(nameof(blockNumber));
            return base.SendRequestAsync(id, blockNumber);
        }

        public RpcRequest BuildRequest(HexBigInteger blockNumber, object id = null)
        {
            if (blockNumber == null) throw new ArgumentNullException(nameof(blockNumber));
            return base.BuildRequest(id, blockNumber);
        }
    }
}