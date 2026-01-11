using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.Signer;

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

        private static string GetSenderAddress(ISignedTransaction tx)
        {
            try
            {
                var signature = tx.Signature;
                if (signature == null) return null;

                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key != null ? key.GetPublicAddress() : null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetReceiverAddress(ISignedTransaction tx)
        {
            if (tx == null) return null;

            if (tx is LegacyTransaction legacy)
            {
                var addr = legacy.ReceiveAddress;
                return addr != null && addr.Length > 0 ? addr.ToHex(true) : null;
            }
            if (tx is LegacyTransactionChainId legacyChainId)
            {
                var addr = legacyChainId.ReceiveAddress;
                return addr != null && addr.Length > 0 ? addr.ToHex(true) : null;
            }
            if (tx is Transaction1559 eip1559)
                return eip1559.ReceiverAddress;
            if (tx is Transaction2930 eip2930)
                return eip2930.ReceiverAddress;

            return null;
        }
    }
}
