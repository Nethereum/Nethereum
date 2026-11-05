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
            var blockTag = GetOptionalBlockParam(request, 2, "latest");

            if (slotHex != null && slotHex.StartsWith("0x") && slotHex.Length - 2 > 64)
                throw RpcException.InvalidParams(
                    $"storage key too long (want at most 32 bytes): \"{slotHex}\"");

            if (slotHex == null || !IsHex(slotHex.StartsWith("0x") ? slotHex.Substring(2) : slotHex))
                throw RpcException.InvalidParams($"invalid hex in storage key: \"{slotHex}\"");

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

        private static bool IsHex(string text)
        {
            foreach (var c in text)
                if (!System.Uri.IsHexDigit(c)) return false;
            return true;
        }
    }
}
