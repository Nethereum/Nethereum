using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Quorum.RPC
{
    public interface IQuorumVote
    {
        RpcRequest BuildRequest(string hash, object id = null);
        Task<string> SendRequestAsync(string hash, object id = null);
    }
}