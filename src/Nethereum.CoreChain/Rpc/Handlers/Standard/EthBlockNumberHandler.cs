using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthBlockNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_blockNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockNumber = await context.Node.GetBlockNumberAsync();
            return Success(request.Id, new HexBigInteger(blockNumber));
        }
    }
}
