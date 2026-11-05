using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Rsk.RPC.RskEth.DTOs;

namespace Nethereum.Rsk.RPC.RskEth
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "rsk-client", "IRskEthGetBlockWithTransactionsByHash")]
    public interface IRskEthGetBlockWithTransactionsByHash
    {
        Task<RskBlockWithTransactions> SendRequestAsync(string blockHash, object id = null);
        RpcRequest BuildRequest(string blockHash, object id = null);
    }
}