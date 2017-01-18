using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC
{
    public class QuorumResumeBlockMaker : GenericRpcRequestResponseHandlerNoParam<object>
    {
        public QuorumResumeBlockMaker(IClient client) : base(client, ApiMethods.quorum_resumeBlockMaker.ToString())
        {
        }
    }
}