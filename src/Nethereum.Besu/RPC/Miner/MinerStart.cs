using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Besu.RPC.Miner
{
    /// <Summary>
    ///     Starts the CPU mining process. To start mining, a miner coinbase must have been previously specified using the
    ///     --miner-coinbase command line option.
    /// </Summary>
    public class MinerStart : GenericRpcRequestResponseHandlerNoParam<bool>, IMinerStart
    {
        public MinerStart(IClient client) : base(client, ApiMethods.miner_start.ToString())
        {
        }
    }
}