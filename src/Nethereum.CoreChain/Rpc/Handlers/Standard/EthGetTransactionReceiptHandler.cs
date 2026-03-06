using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionReceiptHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionReceipt.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var hash = hashHex.HexToByteArray();

            var receiptInfo = await context.Node.GetTransactionReceiptInfoAsync(hash);
            if (receiptInfo == null)
            {
                return Success(request.Id, null);
            }

            var tx = await context.Node.GetTransactionByHashAsync(hash);
            var from = tx != null ? GetSenderAddress(tx) : null;
            var to = GetReceiverAddress(tx);

            var receipt = receiptInfo.ToTransactionReceipt(from, to);
            return Success(request.Id, receipt);
        }
    }
}
