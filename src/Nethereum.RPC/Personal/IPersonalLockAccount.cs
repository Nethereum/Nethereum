using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Personal
{
    public interface IPersonalLockAccount
    {
        RpcRequest BuildRequest(string account, object id = null);
        Task<bool> SendRequestAsync(string account, object id = null);
    }
}