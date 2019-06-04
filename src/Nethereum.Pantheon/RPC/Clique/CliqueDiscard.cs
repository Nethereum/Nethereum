using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Pantheon.RPC.Clique
{
    /// <Summary>
    ///     Discards a proposal to add or remove a signer with the specified address.
    /// </Summary>
    public class CliqueDiscard : RpcRequestResponseHandler<bool>, ICliqueDiscard
    {
        public CliqueDiscard(IClient client) : base(client, ApiMethods.clique_discard.ToString())
        {
        }

        public async Task<bool> SendRequestAsync(string addressSigner, object id = null)
        {
            return await base.SendRequestAsync(id, addressSigner);
        }

        public RpcRequest BuildRequest(string addressSigner, object id = null)
        {
            return base.BuildRequest(id, addressSigner);
        }
    }
}