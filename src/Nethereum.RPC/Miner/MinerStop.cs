
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC
{

    ///<Summary>
    /// Stop the CPU mining operation.    
    ///</Summary>
    public class MinerStop : GenericRpcRequestResponseHandlerNoParam<bool>
    {
            public MinerStop(IClient client) : base(client, ApiMethods.miner_stop.ToString()) { }
    }

}
            
        