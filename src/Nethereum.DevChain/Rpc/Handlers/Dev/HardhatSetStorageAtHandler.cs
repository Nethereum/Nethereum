using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class HardhatSetStorageAtHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.hardhat_setStorageAt.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var address = GetParam<string>(request, 0);
            var slotHex = GetParam<string>(request, 1);
            var valueHex = GetParam<string>(request, 2);

            var slot = slotHex.HexToBigInteger(false);
            var value = valueHex.HexToByteArray();

            await devNode.SetStorageAtAsync(address, slot, value);

            return Success(request.Id, true);
        }
    }
}
