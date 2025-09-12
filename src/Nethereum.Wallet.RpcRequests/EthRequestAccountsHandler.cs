using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthRequestAccountsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_requestAccounts";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var selected = context.SelectedWalletAccount?.Address ?? "0x0000000000000000000000000000000000000000";
            var accounts = new[] { selected };
            var result = JArray.FromObject(accounts);
            return Task.FromResult(new RpcResponseMessage(request.Id, result));
        }
    }

}
