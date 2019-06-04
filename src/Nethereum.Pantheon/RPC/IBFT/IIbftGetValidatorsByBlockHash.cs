using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

public interface IIbftGetValidatorsByBlockHash
{
    Task<string[]> SendRequestAsync(string blockHash, object id = null);
    RpcRequest BuildRequest(string blockHash, object id = null);
}