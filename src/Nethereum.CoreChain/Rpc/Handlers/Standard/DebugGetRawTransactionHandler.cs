using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugGetRawTransactionHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.debug_getRawTransaction.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            if (string.IsNullOrEmpty(hashHex) || !hashHex.StartsWith("0x"))
                throw RpcException.InvalidParams("invalid argument 0: hex string without 0x prefix");

            var tx = await context.Node.GetTransactionByHashAsync(hashHex.HexToByteArray());
            if (tx == null)
                return Success(request.Id, null);

            return Success(request.Id, tx.GetRLPEncoded().ToHex(true));
        }
    }
}
