using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugGetRawReceiptsHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.debug_getRawReceipts.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var receipts = await context.Node.Receipts.GetByBlockNumberAsync(blockNumber);
            if (receipts == null)
                return Success(request.Id, new string[0]);

            var encoded = receipts
                .Select(r => RlpBlockEncodingProvider.Instance.EncodeReceipt(r).ToHex(true))
                .ToArray();
            return Success(request.Id, encoded);
        }
    }
}
