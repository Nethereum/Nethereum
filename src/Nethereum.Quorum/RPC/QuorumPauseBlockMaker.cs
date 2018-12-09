using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC
{
    public class QuorumPauseBlockMaker : GenericRpcRequestResponseHandlerNoParam<object>, IQuorumPauseBlockMaker
    {
        public QuorumPauseBlockMaker(IClient client) : base(client, ApiMethods.quorum_pauseBlockMaker.ToString())
        {
        }
    }

    public interface IQuorumPauseBlockMaker : IGenericRpcRequestResponseHandlerNoParam<object>
    {

    }
}