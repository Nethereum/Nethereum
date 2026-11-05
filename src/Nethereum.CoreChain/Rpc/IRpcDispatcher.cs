using Nethereum.Documentation;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "IRpcDispatcher - the seam every JSON-RPC front end dispatches through")]
    public interface IRpcDispatcher
    {
        int MaxBatchSize { get; set; }

        Task<RpcResponseMessage> DispatchAsync(RpcRequestMessage request);

        Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests);
    }
}
