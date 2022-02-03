using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC
{
    public class QuorumNodeInfo : GenericRpcRequestResponseHandlerNoParam<NodeInfo>, IQuorumNodeInfo
    {
        public QuorumNodeInfo(IClient client) : base(client, ApiMethods.quorum_nodeInfo.ToString())
        {
        }
    }

    public interface IQuorumNodeInfo : IGenericRpcRequestResponseHandlerNoParam<NodeInfo>
    {

    }


}