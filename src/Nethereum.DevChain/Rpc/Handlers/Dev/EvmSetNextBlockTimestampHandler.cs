using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class EvmSetNextBlockTimestampHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.evm_setNextBlockTimestamp.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var timestamp = ParseHexOrDecimalLong(request, 0);
            var devNode = (DevChainNode)context.Node;
            devNode.DevConfig.SetNextBlockTimestamp(timestamp);
            return Task.FromResult(Success(request.Id, ToHex(timestamp)));
        }
    }
}
