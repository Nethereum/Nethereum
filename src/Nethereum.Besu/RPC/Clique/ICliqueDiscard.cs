using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.Clique
{
    public interface ICliqueDiscard
    {
        Task<bool> SendRequestAsync(string addressSigner, object id = null);
        RpcRequest BuildRequest(string addressSigner, object id = null);
    }
}