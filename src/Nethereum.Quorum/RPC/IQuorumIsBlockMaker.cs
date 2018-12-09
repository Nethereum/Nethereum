using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Quorum.RPC
{
    public interface IQuorumIsBlockMaker
    {
        RpcRequest BuildRequest(string address, object id = null);
        Task<bool> SendRequestAsync(string address, object id = null);
    }
}