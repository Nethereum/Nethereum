using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthSendRawTransactionHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_sendRawTransaction.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var rawTxHex = GetParam<string>(request, 0);

            ISignedTransaction signedTx;
            try
            {
                var rawTxBytes = rawTxHex.HexToByteArray();
                signedTx = TransactionFactory.CreateTransaction(rawTxBytes);
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32000, $"Invalid transaction: {ex.Message}");
            }

            var result = await context.Node.SendTransactionAsync(signedTx);

            if (result.Success || result.Receipt != null)
            {
                return Success(request.Id, result.TransactionHash.ToHex(true));
            }

            return Error(request.Id, -32000, result.RevertReason ?? "Transaction rejected");
        }
    }
}
