using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.IBFT
{
    public interface IIbftProposeValidatorVote
    {
        Task<bool> SendRequestAsync(string accountAddress, bool addValidator, object id = null);
        RpcRequest BuildRequest(string accountAddress, bool addValidator, object id = null);
    }
}