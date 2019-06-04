using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

public interface IIbftDiscardValidatorVote
{
    Task<bool> SendRequestAsync(string validatorAddress, object id = null);
    RpcRequest BuildRequest(string validatorAddress, object id = null);
}