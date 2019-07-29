using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Pantheon.RPC.IBFT
{
    public interface IIbftDiscardValidatorVote
    {
        Task<bool> SendRequestAsync(string validatorAddress, object id = null);
        RpcRequest BuildRequest(string validatorAddress, object id = null);
    }
}