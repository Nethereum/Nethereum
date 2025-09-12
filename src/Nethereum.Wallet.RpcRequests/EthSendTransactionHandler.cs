using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.UI;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthSendTransactionHandler : RpcMethodHandlerBase
    {
        public EthSendTransactionHandler()
        {
            
        }

        public override string MethodName => "eth_sendTransaction";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var input = request.GetFirstParamAs<TransactionInput>();
            if (input == null)
                return InvalidParams(request.Id);

            if (string.IsNullOrEmpty(input.From))
            {
                input.From = context.SelectedWalletAccount?.Address;
                if (string.IsNullOrEmpty(input.From))
                    return InvalidParams(request.Id, "Missing 'from' address.");
            }

            var txHash = await context.ShowTransactionDialogAsync(input);
            if (string.IsNullOrEmpty(txHash))
                return UserRejected(request.Id);

            return new RpcResponseMessage(request.Id, txHash);
        }
    }

}
