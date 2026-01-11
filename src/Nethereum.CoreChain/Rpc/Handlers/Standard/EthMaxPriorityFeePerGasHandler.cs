using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthMaxPriorityFeePerGasHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_maxPriorityFeePerGas.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var priorityFee = context.Node.Config.SuggestedPriorityFee;
            return Task.FromResult(Success(request.Id, new HexBigInteger(priorityFee)));
        }
    }
}
