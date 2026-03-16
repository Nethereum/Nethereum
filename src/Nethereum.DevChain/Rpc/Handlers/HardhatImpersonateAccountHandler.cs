using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Accounts;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.DevChain.Rpc.Handlers
{
    public class HardhatImpersonateAccountHandler : RpcHandlerBase
    {
        public override string MethodName => "hardhat_impersonateAccount";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0);
            var accountManager = context.GetRequiredService<DevAccountManager>();
            accountManager.ImpersonateAccount(address);
            return Task.FromResult(Success(request.Id, true));
        }
    }
}
