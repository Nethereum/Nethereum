using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class HardhatSetNonceHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.hardhat_setNonce.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var address = GetParam<string>(request, 0);
            var nonceHex = GetParam<string>(request, 1);
            var nonce = nonceHex.HexToBigInteger(false);

            await devNode.SetNonceAsync(address, nonce);

            return Success(request.Id, true);
        }
    }
}
