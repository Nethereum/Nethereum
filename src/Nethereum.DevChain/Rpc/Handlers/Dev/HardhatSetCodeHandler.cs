using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class HardhatSetCodeHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.hardhat_setCode.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var address = GetParam<string>(request, 0);
            var codeHex = GetParam<string>(request, 1);
            var code = codeHex.HexToByteArray();

            await devNode.SetCodeAsync(address, code);

            return Success(request.Id, true);
        }
    }
}
