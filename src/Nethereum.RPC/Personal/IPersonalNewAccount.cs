using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Personal
{
    public interface IPersonalNewAccount
    {
        RpcRequest BuildRequest(string passPhrase, object id = null);
        Task<string> SendRequestAsync(string passPhrase, object id = null);
    }
}