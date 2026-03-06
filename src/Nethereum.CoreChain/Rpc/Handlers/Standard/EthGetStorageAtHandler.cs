using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.Util;

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

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);
            var slot = slotHex.HexToBigInteger(false);

            var value = await context.Node.GetStorageAtAsync(address, slot, blockNumber);

            if (value == null || value.Length == 0)
            {
                return Success(request.Id, "0x0000000000000000000000000000000000000000000000000000000000000000");
            }

            var paddedValue = value.PadBytes(32);
            return Success(request.Id, paddedValue.ToHex(true));
        }
    }
}
