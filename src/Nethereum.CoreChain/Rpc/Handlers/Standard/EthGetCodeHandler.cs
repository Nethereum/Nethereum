using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetCodeHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getCode.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0);
            var code = await context.Node.GetCodeAsync(address);
            return Success(request.Id, code != null ? code.ToHex(true) : "0x");
        }
    }
}
