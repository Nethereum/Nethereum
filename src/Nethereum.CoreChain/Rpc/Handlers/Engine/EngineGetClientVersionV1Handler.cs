using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineGetClientVersionV1Handler : RpcHandlerBase
    {
        public const string ClientCode = "NE";
        public const string ClientName = "Nethereum";
        public const string UnknownCommit = "unknown";

        public override string MethodName => "engine_getClientVersionV1";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var response = new List<ClientVersionV1>
            {
                new ClientVersionV1
                {
                    Code = ClientCode,
                    Name = ClientName,
                    Version = "v" + NodeVersion.Version,
                    Commit = NodeVersion.CommitPrefix ?? UnknownCommit
                }
            };

            return Task.FromResult(Success(request.Id, response));
        }
    }
}
