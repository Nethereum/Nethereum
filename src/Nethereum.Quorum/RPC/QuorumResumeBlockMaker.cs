using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC
{
    public class QuorumResumeBlockMaker : GenericRpcRequestResponseHandlerNoParam<object>, IQuorumResumeBlockMaker
    {
        public QuorumResumeBlockMaker(IClient client) : base(client, ApiMethods.quorum_resumeBlockMaker.ToString())
        {
        }
    }

    public interface IQuorumResumeBlockMaker : IGenericRpcRequestResponseHandlerNoParam<object>
    {

    }
}