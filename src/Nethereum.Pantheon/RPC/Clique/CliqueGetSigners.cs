using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Pantheon.RPC.Clique
{
    public interface ICliqueGetSigners
    {
        Task<string[]> SendRequestAsync(BlockParameter blockParameter, object id = null);
        RpcRequest BuildRequest(BlockParameter blockParameter, object id = null);
    }

    /// <Summary>
    ///     Lists signers for the specified block.
    /// </Summary>
    public class CliqueGetSigners : RpcRequestResponseHandler<string[]>, ICliqueGetSigners
    {
        public CliqueGetSigners(IClient client) : base(client, ApiMethods.clique_getSigners.ToString())
        {
        }

        public async Task<string[]> SendRequestAsync(BlockParameter blockParameter, object id = null)
        {
            return await base.SendRequestAsync(id, blockParameter);
        }

        public RpcRequest BuildRequest(BlockParameter blockParameter, object id = null)
        {
            return base.BuildRequest(id, blockParameter);
        }
    }
}