using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Geth.RPC.Miner
{
    /// <Summary>
    ///     Stop the CPU mining operation.
    /// </Summary>
    public class MinerStop : GenericRpcRequestResponseHandlerNoParam<bool>, IMinerStop
    {
        public MinerStop(IClient client) : base(client, ApiMethods.miner_stop.ToString())
        {
        }
    }
}