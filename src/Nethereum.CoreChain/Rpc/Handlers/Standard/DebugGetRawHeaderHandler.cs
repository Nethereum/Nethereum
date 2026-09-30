using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugGetRawHeaderHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.debug_getRawHeader.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var header = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (header == null)
                return Success(request.Id, null);

            return Success(request.Id, BlockHeaderEncoder.Current.Encode(header).ToHex(true));
        }
    }
}
