using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC
{
    public class QuorumMakeBlock : GenericRpcRequestResponseHandlerNoParam<string>, IQuorumMakeBlock
    {
        public QuorumMakeBlock(IClient client) : base(client, ApiMethods.quorum_makeBlock.ToString())
        {
        }
    }

    public interface IQuorumMakeBlock : IGenericRpcRequestResponseHandlerNoParam<string>
    {

    }
}