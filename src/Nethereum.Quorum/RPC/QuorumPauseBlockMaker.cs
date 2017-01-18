using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC
{
    public class QuorumPauseBlockMaker : GenericRpcRequestResponseHandlerNoParam<object>
    {
        public QuorumPauseBlockMaker(IClient client) : base(client, ApiMethods.quorum_pauseBlockMaker.ToString())
        {
        }
    }
}