using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Pantheon.RPC.Miner
{
    /// <Summary>
    ///     Stops the CPU mining process on the client.
    /// </Summary>
    public class MinerStop : GenericRpcRequestResponseHandlerNoParam<bool>, IMinerStop
    {
        public MinerStop(IClient client) : base(client, ApiMethods.miner_stop.ToString())
        {
        }
    }
}