using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetStorageAtHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getStorageAt.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0);
            var slotHex = GetParam<string>(request, 1);
            var blockTag = GetOptionalParam<string>(request, 2, "latest");

            ValidateBlockParameterIsLatest(blockTag, MethodName);

            var slot = slotHex.HexToBigInteger(false);

            var value = await context.Node.GetStorageAtAsync(address, slot);

            if (value == null || value.Length == 0)
            {
                return Success(request.Id, "0x0000000000000000000000000000000000000000000000000000000000000000");
            }

            var paddedValue = new byte[32];
            System.Array.Copy(value, 0, paddedValue, 32 - value.Length, value.Length);
            return Success(request.Id, paddedValue.ToHex(true));
        }
    }
}
