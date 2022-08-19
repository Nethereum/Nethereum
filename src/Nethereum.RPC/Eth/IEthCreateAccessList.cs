using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth
{
    public interface IEthCreateAccessList
    {
        BlockParameter DefaultBlock { get; set; }

        RpcRequest BuildRequest(TransactionInput transactionInput, BlockParameter block, object id = null);
        Task<AccessListGasUsed> SendRequestAsync(TransactionInput transactionInput, object id = null);
        Task<AccessListGasUsed> SendRequestAsync(TransactionInput transactionInput, BlockParameter block, object id = null);
    }
}