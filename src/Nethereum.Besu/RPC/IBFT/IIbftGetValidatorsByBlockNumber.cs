using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Besu.RPC.IBFT
{
    public interface IIbftGetValidatorsByBlockNumber
    {
        Task<string[]> SendRequestAsync(BlockParameter block, object id = null);
        RpcRequest BuildRequest(BlockParameter block, object id = null);
    }
}