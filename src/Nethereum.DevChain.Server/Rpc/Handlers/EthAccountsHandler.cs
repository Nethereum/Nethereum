using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.DevChain.Server.Rpc.Handlers
{
    public class EthAccountsHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_accounts.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var accountManager = context.GetRequiredService<DevAccountManager>();
            var addresses = accountManager.GetAllAddresses();
            return Task.FromResult(Success(request.Id, addresses));
        }
    }
}
